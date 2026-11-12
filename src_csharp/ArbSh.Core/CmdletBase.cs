using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace ArbSh.Core
{
    /// <summary>
    /// Base class for all cmdlets. Provides common functionality.
    /// Inspired by System.Management.Automation.Cmdlet.
    /// </summary>
    public abstract class CmdletBase
    {
        /// <summary>
        /// Internal collection for cmdlet output. The Executor assigns this.
        /// </summary>
        internal BlockingCollection<PipelineObject>? OutputCollection { get; set; }

        /// <summary>
        /// Called once before ProcessRecord is invoked for the first time.
        /// Override for one-time initialization tasks.
        /// </summary>
        public virtual void BeginProcessing() { }

        /// <summary>
        /// Called for each object piped into the cmdlet.
        /// Override to implement the main processing logic for each input object.
        /// If the cmdlet doesn't accept pipeline input, this might be called once with null.
        /// </summary>
        /// <param name="input">The input object from the pipeline (can be null).</param>
        public virtual void ProcessRecord(PipelineObject? input) { }

        /// <summary>
        /// Called once after all calls to ProcessRecord are complete.
        /// Override for one-time cleanup or final output tasks.
        /// </summary>
        public virtual void EndProcessing() { }

        /// <summary>
        /// Writes a single object to the output pipeline.
        /// </summary>
        /// <param name="output">The object to write.</param>
        protected void WriteObject(object? output)
        {
            if (output is PipelineObject { IsError: true })
            {
                WroteError = true;
            }

            if (OutputCollection != null && !OutputCollection.IsAddingCompleted)
            {
                try
                {
                    OutputCollection.Add(output as PipelineObject ?? new PipelineObject(output));
                }
                catch (InvalidOperationException)
                {
                    // Ignore if collection was completed concurrently, though ideally Executor manages this.
                    CoreConsole.WriteLine("WARN (CmdletBase): Attempted to write to completed output collection.");
                }
            }
            else
            {
                // Fallback or error? If OutputCollection is null, something is wrong in Executor setup.
                // If IsAddingCompleted, the cmdlet is trying to write after EndProcessing or after pipeline completion signal.
                CoreConsole.WriteLine($"WARN (CmdletBase): OutputCollection not available or completed. Cannot write object: {output}");
            }
        }

        /// <summary>
        /// Writes multiple objects to the output pipeline.
        /// </summary>
        /// <param name="output">The collection of objects to write.</param>
        /// <param name="enumerateCollection">If true, enumerates the collection and writes each element individually.</param>
        protected void WriteObject(IEnumerable<object?> output, bool enumerateCollection)
        {
            if (!enumerateCollection)
            {
                WriteObject((object?)output);
            }
            else
            {
                foreach (var item in output)
                {
                    WriteObject(item);
                }
            }
        }

        /// <summary>
        /// هل كتب الأمر كائن خطأ؟ عندها يخرج برمز <see cref="HostExitCodes.CommandFailed"/>.
        /// </summary>
        internal bool WroteError { get; private set; }

        // TODO: Add WriteError, WriteWarning, WriteVerbose, WriteDebug methods

        /// <summary>
        /// Internal method called by the Executor before ProcessRecord for each pipeline object.
        /// Attempts to bind pipeline input to parameters marked with ValueFromPipeline=true
        /// or ValueFromPipelineByPropertyName=true.
        /// </summary>
        /// <param name="input">The current pipeline input object.</param>
        internal virtual void BindPipelineParameters(PipelineObject? input)
        {
            if (input?.Value == null) return; // Nothing to bind from

            var cmdletType = this.GetType();
            var properties = cmdletType.GetProperties()
                .Select(p => new { Property = p, Attr = p.GetCustomAttribute<ParameterAttribute>() })
                .Where(p => p.Attr != null && (p.Attr.ValueFromPipeline || p.Attr.ValueFromPipelineByPropertyName))
                .ToList();

            if (!properties.Any()) return; // No pipeline parameters defined

            object inputValue = input.Value;
            Type inputType = inputValue.GetType();

            // CoreConsole.WriteLine($"DEBUG (BindPipeline): Attempting pipeline binding for input type {inputType?.Name ?? "null"} to {cmdletType.Name}");

            foreach (var propInfo in properties)
            {
                object? valueToSet = null;
                bool bound = false;

                // 1. Try ValueFromPipeline = true
                if (propInfo.Attr!.ValueFromPipeline)
                {
                    // Check if the target property type is assignable from the input object's type
                    if (propInfo.Property.PropertyType.IsAssignableFrom(inputType))
                    {
                        valueToSet = inputValue;
                        bound = true;
                        // CoreConsole.WriteLine($"DEBUG (BindPipeline): Bound '{propInfo.Property.Name}' ByValue.");
                    }
                    else
                    {
                        // Attempt type conversion if direct assignment isn't possible
                        try
                        {
                            TypeConverter converter = TypeDescriptor.GetConverter(propInfo.Property.PropertyType);
                            if (converter != null && converter.CanConvertFrom(inputType!))
                            {
                                valueToSet = converter.ConvertFrom(inputValue);
                                bound = true;
                                // CoreConsole.WriteLine($"DEBUG (BindPipeline): Converted and bound '{propInfo.Property.Name}' ByValue.");
                            }
                            else if (inputValue is IConvertible) // Fallback using IConvertible
                            {
                                valueToSet = Convert.ChangeType(inputValue, propInfo.Property.PropertyType);
                                bound = true;
                                // CoreConsole.WriteLine($"DEBUG (BindPipeline): ChangeType and bound '{propInfo.Property.Name}' ByValue.");
                            }
                        }
                        catch (Exception ex)
                        {
                            CoreConsole.WriteLine($"WARN (BindPipeline): Failed to convert pipeline input type '{inputType?.Name}' to parameter '{propInfo.Property.Name}' type '{propInfo.Property.PropertyType.Name}' for ByValue binding. Error: {ex.Message}");
                        }
                    }
                }

                // 2. Try ValueFromPipelineByPropertyName = true (only if not already bound by value)
                if (!bound && propInfo.Attr!.ValueFromPipelineByPropertyName && inputType != null)
                {
                    // Find a property on the *input object* that matches the *parameter name*
                    PropertyInfo? inputObjectProperty = inputType.GetProperty(propInfo.Property.Name, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

                    if (inputObjectProperty != null)
                    {
                        object? sourceValue = inputObjectProperty.GetValue(inputValue);

                        // Check if the target parameter property type is assignable from the source property type
                        if (sourceValue != null && propInfo.Property.PropertyType.IsAssignableFrom(inputObjectProperty.PropertyType))
                        {
                            valueToSet = sourceValue;
                            bound = true;
                            // CoreConsole.WriteLine($"DEBUG (BindPipeline): Bound '{propInfo.Property.Name}' ByPropertyName.");
                        }
                        else if (sourceValue != null) // Attempt conversion
                        {
                            try
                            {
                                TypeConverter converter = TypeDescriptor.GetConverter(propInfo.Property.PropertyType);
                                if (converter != null && converter.CanConvertFrom(inputObjectProperty.PropertyType))
                                {
                                    valueToSet = converter.ConvertFrom(sourceValue);
                                    bound = true;
                                    // CoreConsole.WriteLine($"DEBUG (BindPipeline): Converted and bound '{propInfo.Property.Name}' ByPropertyName.");
                                }
                                else if (sourceValue is IConvertible)
                                {
                                    valueToSet = Convert.ChangeType(sourceValue, propInfo.Property.PropertyType);
                                    bound = true;
                                    // CoreConsole.WriteLine($"DEBUG (BindPipeline): ChangeType and bound '{propInfo.Property.Name}' ByPropertyName.");
                                }
                            }
                            catch (Exception ex)
                            {
                                CoreConsole.WriteLine($"WARN (BindPipeline): Failed to convert pipeline input property '{inputObjectProperty.Name}' type '{inputObjectProperty.PropertyType.Name}' to parameter '{propInfo.Property.Name}' type '{propInfo.Property.PropertyType.Name}' for ByPropertyName binding. Error: {ex.Message}");
                            }
                        }
                        else if (sourceValue == null && propInfo.Property.PropertyType.IsClass || Nullable.GetUnderlyingType(propInfo.Property.PropertyType) != null)
                        {
                            // Allow setting null if the source property is null and the target is nullable/class
                            valueToSet = null;
                            bound = true;
                            // CoreConsole.WriteLine($"DEBUG (BindPipeline): Bound null '{propInfo.Property.Name}' ByPropertyName.");
                        }
                    }
                }

                // 3. Set the property value if bound
                if (bound)
                {
                    try
                    {
                        propInfo.Property.SetValue(this, valueToSet);
                    }
                    catch (Exception ex)
                    {
                        // Error setting the property value
                        CoreConsole.WriteLine($"ERROR (BindPipeline): Failed to set pipeline-bound property '{propInfo.Property.Name}': {ex.Message}");
                    }
                }
            }
        }
    }
}


