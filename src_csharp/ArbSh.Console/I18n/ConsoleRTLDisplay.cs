using System;
using System.Text;
using ArbSh.Core.I18n;

namespace ArbSh.Console.I18n
{
    /// <summary>
    /// Handles RTL (Right-to-Left) display formatting for console output.
    /// Addresses Windows Console limitations with Arabic text positioning and character shaping.
    /// </summary>
    public static class ConsoleRTLDisplay
    {
        #region Console Properties
        
        /// <summary>
        /// Gets the current console width for RTL positioning calculations.
        /// </summary>
        private static int ConsoleWidth
        {
            get
            {
                try
                {
                    return System.Console.WindowWidth;
                }
                catch
                {
                    // Fallback if console width cannot be determined
                    return 80;
                }
            }
        }
        
        #endregion

        #region RTL Prompt Handling
        
        /// <summary>
        /// Formats and displays the shell prompt with proper RTL positioning.
        /// For Arabic context, we need to balance RTL appearance with console input behavior.
        /// </summary>
        /// <param name="promptText">The prompt text (e.g., "ArbSh>")</param>
        /// <param name="forceRTL">Force RTL positioning even for non-Arabic prompts</param>
        public static void DisplayRTLPrompt(string promptText, bool forceRTL = false)
        {
            if (string.IsNullOrEmpty(promptText))
            {
                return;
            }

            // Check if we should use RTL positioning
            bool useRTL = forceRTL || BiDiTextProcessor.ContainsArabicText(promptText);

            if (useRTL)
            {
                DisplayRTLAwarePrompt(promptText);
            }
            else
            {
                // Standard left-aligned prompt for LTR content
                System.Console.Write(promptText);
            }
        }

        /// <summary>
        /// Displays an RTL-aware prompt that works properly with console input behavior.
        /// For Arabic prompts, we want RTL display but cursor positioning that works with console input.
        /// </summary>
        /// <param name="promptText">The prompt text to display</param>
        private static void DisplayRTLAwarePrompt(string promptText)
        {
            try
            {
                // Process text for RTL display (Shape -> BiDi)
                string processedPrompt = ProcessTextForRTLDisplay(promptText);
                
                // Calculate console width
                int consoleWidth = ConsoleWidth;
                int promptLength = GetDisplayLength(processedPrompt);

                // Right alignment calculation
                // We want the prompt to end at the right edge
                // Padded: "                                   >أربش"
                
                int padding = Math.Max(0, consoleWidth - promptLength - 1); // -1 for buffer/safety
                
                // Construct line
                // Note: In RTL view, we print padding THEN prompt.
                // But wait, if we print padding, the cursor ends up at the right.
                // Standard Console.ReadLine starts wherever the cursor is.
                // This is perfect for RTL typing!
                
                string paddedPrompt = new string(' ', padding) + processedPrompt;
                
                // Write without newline so input happens on same line
                System.Console.Write(paddedPrompt);
                
                // Note: Windows Console cursor will be at the end of the line (Right side).
                // This mimics RTL input start position.
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"DEBUG (RTL): RTL prompt display failed: {ex.Message}");
                // Fallback to normal prompt display
                System.Console.Write(promptText);
            }
        }
        
        #endregion

        #region Arabic Character Shaping
        
        /// <summary>
        /// Applies Arabic character shaping and connection rules for better display.
        /// Uses ICU4N to convert logical characters to presentation forms.
        /// </summary>
        /// <param name="arabicText">Arabic text to shape</param>
        /// <returns>Text with improved Arabic character shaping</returns>
        public static string ShapeArabicText(string arabicText)
        {
            if (string.IsNullOrEmpty(arabicText))
            {
                return arabicText;
            }

            // Check if text contains Arabic characters
            if (!BiDiTextProcessor.ContainsArabicText(arabicText))
            {
                return arabicText;
            }

            try
            {
                // Use the ArabicShaper wrapper to perform ICU4N shaping
                string shaped = ArabicShaper.Shape(arabicText);
                
                return shaped;
            }
            catch (Exception)
            {
                return arabicText;
            }
        }
        
        #endregion

        #region RTL Text Display
        
        /// <summary>
        /// Displays text with proper RTL formatting and positioning.
        /// </summary>
        /// <param name="text">Text to display</param>
        /// <param name="rightAlign">Whether to right-align the text</param>
        public static void DisplayRTLText(string text, bool rightAlign = true)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // A host reading our output (Qalam's panel, a file) does its own BiDi, so keep logical order.
            if (System.Console.IsOutputRedirected)
            {
                System.Console.WriteLine(text);
                return;
            }

            // Process text for display (BiDi + shaping)
            string processedText = ProcessTextForRTLDisplay(text);
            
            if (rightAlign && BiDiTextProcessor.ContainsArabicText(text))
            {
                DisplayRightAlignedText(processedText);
            }
            else
            {
                System.Console.WriteLine(processedText);
            }
        }

        /// <summary>
        /// Processes text for RTL display with Shaping AND BiDi reordering.
        /// </summary>
        /// <param name="text">Text to process</param>
        /// <returns>Text ready for RTL display</returns>
        public static string ProcessTextForRTLDisplay(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            // IMPORTANT PIPELINE ORDER:
            // 1. Shaping (Logical -> Logical Presentation Forms)
            // 2. BiDi Reordering (Logical -> Visual)
            
            // يجب تشكيل الأحرف أولاً لأن التشكيل يعتمد على الجوار المنطقي
            // Shaping must happen BEFORE reordering because it depends on logical adjacency.
            string shaped = ShapeArabicText(text);

            // ثم نقوم بتطبيق خوارزمية الاتجاهين لعرض النص بشكل صحيح
            // Then apply BiDi algorithm for proper display ordering
            string visual = BiDiTextProcessor.ProcessOutputForDisplay(shaped);
            
            return visual;
        }

        /// <summary>
        /// Displays text with right alignment.
        /// </summary>
        /// <param name="text">Text to display</param>
        private static void DisplayRightAlignedText(string text)
        {
            try
            {
                string[] lines = text.Split('\n');
                
                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        System.Console.WriteLine();
                        continue;
                    }

                    int lineLength = GetDisplayLength(line);
                    int consoleWidth = ConsoleWidth;
                    
                    if (lineLength < consoleWidth)
                    {
                        int padding = consoleWidth - lineLength;
                        string paddedLine = new string(' ', padding) + line;
                        System.Console.WriteLine(paddedLine);
                    }
                    else
                    {
                        System.Console.WriteLine(line);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"DEBUG (RTL): Right alignment failed: {ex.Message}");
                System.Console.WriteLine(text);
            }
        }
        
        #endregion

        #region Utility Methods
        
        /// <summary>
        /// Gets the display length of text, accounting for Unicode characters.
        /// </summary>
        /// <param name="text">Text to measure</param>
        /// <returns>Display length</returns>
        private static int GetDisplayLength(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            // Simple implementation - count characters
            // TODO: Improve to handle combining characters, wide characters, etc.
            return text.Length;
        }

        /// <summary>
        /// Determines if RTL display mode should be used based on text content.
        /// </summary>
        /// <param name="text">Text to analyze</param>
        /// <returns>True if RTL display should be used</returns>
        public static bool ShouldUseRTLDisplay(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            // Use RTL display if text contains Arabic characters
            return BiDiTextProcessor.ContainsArabicText(text);
        }
        
        #endregion
    }
}
