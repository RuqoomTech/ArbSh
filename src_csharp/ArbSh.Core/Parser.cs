using System;
using System.Collections.Generic;
using System.Linq; // Needed for Skip
using System.Text; // Needed for StringBuilder
using System.Text.RegularExpressions; // Needed for Regex.Match
using ArbSh.Core.Parsing; // Add using for Token types

namespace ArbSh.Core
{
    /// <summary>
    /// Responsible for parsing user input strings into executable commands/structures.
    /// </summary>
    public static class Parser
    {
        // Placeholder for variable storage (replace with proper session state later)
        // TODO: Implement proper session state for variables
        private static Dictionary<string, string> _variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "testVar", "Value from $testVar!" },
            { "pathExample", "C:\\Users" },
            { "emptyVar", "" }
        };

        // Basic variable lookup (replace with proper session state access)
        private static string GetVariableValue(string variableName)
        {
            // Basic lookup, returns empty string if not found (like PowerShell)
            return _variables.TryGetValue(variableName, out var value) ? value : string.Empty;
        }

        // NOTE: Old state machine tokenizer and helper methods removed (IsArabicLetterChar, IsValidIdentifierChar, etc.)
        //       as tokenization is now handled by RegexTokenizer.

        /// <summary>
        /// Parses a line of input into a list of statements, where each statement is a list of commands for a pipeline.
        /// </summary>
        /// <param name="inputLine">The raw input line from the user.</param>
        /// <returns>A list where each element is a list of ParsedCommand objects representing a single statement's pipeline.</returns>
        public static List<List<ParsedCommand>> Parse(string inputLine)
        {
            inputLine = ToSingleLine(inputLine);
            CoreConsole.WriteLine($"DEBUG (Parser): Parsing '{inputLine}'...");
            var allStatementsCommands = new List<List<ParsedCommand>>();
            var statementBuilder = new StringBuilder();
            bool inDoubleQuotes = false;
            bool inSingleQuotes = false; // Add tracking for single quotes
            int start = 0;

            for (int i = 0; i < inputLine.Length; i++)
            {
                char c = inputLine[i];

                // Handle comments: If # is encountered outside quotes, ignore the rest of the line
                if (c == '#' && !inDoubleQuotes && !inSingleQuotes)
                {
                    // If # is the first char of the potential statement, ignore the whole line segment
                    if (i == start || string.IsNullOrWhiteSpace(inputLine.Substring(start, i - start)))
                    {
                        start = inputLine.Length; // Effectively skip the rest
                    }
                    break; // Ignore rest of the current line segment being processed
                }

                // Handle escaping - skip next char if escaped (only applies outside single quotes)
                if (c == '\\' && !inSingleQuotes && i + 1 < inputLine.Length)
                {
                    i++;
                    continue; // Skip escape character logic for statement splitting
                }

                // Toggle quote states
                if (c == '"' && !inSingleQuotes)
                {
                    inDoubleQuotes = !inDoubleQuotes;
                }
                else if (c == '\'' && !inDoubleQuotes) // Add single quote handling
                {
                    inSingleQuotes = !inSingleQuotes;
                }

                // Split statements only if outside *both* types of quotes
                if (c == ';' && !inDoubleQuotes && !inSingleQuotes)
                {
                    string statement = inputLine.Substring(start, i - start).Trim();
                    // Ignore empty statements or statements that were just comments
                    if (!string.IsNullOrWhiteSpace(statement) && !statement.TrimStart().StartsWith("#"))
                    {
                        allStatementsCommands.Add(ParseSingleStatement(statement));
                    }
                    start = i + 1; // Start next statement after the semicolon
                }
            }

            // Add the last statement (or the only statement if no semicolons)
            string lastStatement = inputLine.Substring(start).Trim();
            // Ignore empty statements or statements that were just comments
            if (!string.IsNullOrWhiteSpace(lastStatement) && !lastStatement.TrimStart().StartsWith("#"))
            {
                // Also ignore if the remaining part starts with # after trimming whitespace
                int commentIndex = lastStatement.IndexOf('#');
                if (commentIndex == 0) { /* Ignore */ }
                else if (commentIndex > 0)
                {
                    // If comment is present but not at start, parse the part before it
                    string beforeComment = lastStatement.Substring(0, commentIndex).Trim();
                    if (!string.IsNullOrWhiteSpace(beforeComment))
                    {
                        allStatementsCommands.Add(ParseSingleStatement(beforeComment));
                    }
                }
                else
                {
                    // No comment found
                    allStatementsCommands.Add(ParseSingleStatement(lastStatement));
                }
            }


            // TODO: Handle unterminated quotes at the statement level?
            if (inDoubleQuotes || inSingleQuotes)
            {
                CoreConsole.WriteLine("WARN (Parser): Unterminated quote detected at end of input line.");
                // Potentially throw error or try to recover?
            }


            CoreConsole.WriteLine($"DEBUG (Parser): Parsed into {allStatementsCommands.Count} statement(s).");
            return allStatementsCommands;
        }

        /// <summary>
        /// يحوّل إدخالًا متعدد الأسطر إلى سطر واحد مكافئ: السطر الجديد خارج الاقتباس يفصل الجمل
        /// كـ«;»، إلا بعد «|» أو داخل قوسين حيث يكمل الجملة نفسها؛ ويسقط التعليق حتى نهاية سطره.
        /// </summary>
        public static string ToSingleLine(string input)
        {
            ArgumentNullException.ThrowIfNull(input);
            return input.Contains('\n') ? Scan(input, out _) : input;
        }

        /// <summary>
        /// هل الإدخال ناقص ينتظر سطرًا آخر: اقتباس أو قوس لم يُغلق، أو «|» في آخره.
        /// </summary>
        public static bool IsIncomplete(string input)
        {
            ArgumentNullException.ThrowIfNull(input);
            Scan(input, out bool incomplete);
            return incomplete;
        }

        // يتبع قواعد Parse نفسها للاقتباس والهروب والتعليق كي يتفق الحكم مع التنفيذ.
        private static string Scan(string input, out bool incomplete)
        {
            var result = new StringBuilder(input.Length);
            bool inDoubleQuotes = false;
            bool inSingleQuotes = false;
            int depth = 0;
            char last = '\0';

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                bool quoted = inDoubleQuotes || inSingleQuotes;

                if (!quoted && c == '#')
                {
                    int end = input.IndexOf('\n', i);
                    if (end < 0)
                    {
                        break;
                    }

                    i = end - 1;
                    continue;
                }

                if (!quoted && c == '\r' && i + 1 < input.Length && input[i + 1] == '\n')
                {
                    continue;
                }

                if (!quoted && c == '\n')
                {
                    bool continues = depth > 0 || last is '\0' or '|' or ';';
                    result.Append(continues ? " " : "; ");
                    if (!continues)
                    {
                        last = ';';
                    }

                    continue;
                }

                if (c == '\\' && !inSingleQuotes && i + 1 < input.Length)
                {
                    result.Append(c).Append(input[++i]);
                    last = input[i];
                    continue;
                }

                if (c == '"' && !inSingleQuotes)
                {
                    inDoubleQuotes = !inDoubleQuotes;
                }
                else if (c == '\'' && !inDoubleQuotes)
                {
                    inSingleQuotes = !inSingleQuotes;
                }
                else if (!quoted && c == '(')
                {
                    depth++;
                }
                else if (!quoted && c == ')' && depth > 0)
                {
                    depth--;
                }

                result.Append(c);
                if (!char.IsWhiteSpace(c))
                {
                    last = c;
                }
            }

            incomplete = inDoubleQuotes || inSingleQuotes || depth > 0 || last == '|';
            return result.ToString();
        }

        /// <summary>
        /// Parses a single statement (which might contain a pipeline) into a list of commands.
        /// </summary>
        private static List<ParsedCommand> ParseSingleStatement(string statementInput)
        {
            CoreConsole.WriteLine($"DEBUG (Parser): Processing statement: '{statementInput}'");
            var commandsInStatement = new List<ParsedCommand>();
            int stageStart = 0;
            bool inDoubleQuotes = false; // Track quotes specifically for pipeline splitting
            bool inSingleQuotes = false; // Track single quotes for pipeline splitting

            for (int i = 0; i < statementInput.Length; i++)
            {
                char c = statementInput[i];

                // Handle escaping (only outside single quotes)
                if (c == '\\' && !inSingleQuotes && i + 1 < statementInput.Length)
                {
                    i++; // Skip escaped character for pipeline splitting logic
                    continue;
                }

                // Toggle quote states
                if (c == '"' && !inSingleQuotes)
                {
                    inDoubleQuotes = !inDoubleQuotes;
                }
                else if (c == '\'' && !inDoubleQuotes)
                {
                    inSingleQuotes = !inSingleQuotes;
                }

                // Split pipeline only if outside *both* types of quotes
                if (c == '|' && !inDoubleQuotes && !inSingleQuotes)
                {
                    string stageInput = statementInput.Substring(stageStart, i - stageStart).Trim();
                    if (!string.IsNullOrWhiteSpace(stageInput))
                    {
                        ParsedCommand? command = ParseSinglePipelineStage(stageInput);
                        if (command != null) commandsInStatement.Add(command);
                    }
                    stageStart = i + 1; // Start next stage after the pipe
                }
            }

            // Process the last stage
            string lastStageInput = statementInput.Substring(stageStart).Trim();
            if (!string.IsNullOrWhiteSpace(lastStageInput))
            {
                ParsedCommand? command = ParseSinglePipelineStage(lastStageInput);
                if (command != null) commandsInStatement.Add(command);
            }

            // TODO: Handle unterminated quotes within the statement?

            CoreConsole.WriteLine($"DEBUG (Parser): Statement parsed into {commandsInStatement.Count} pipeline stage(s).");
            return commandsInStatement;
        }

        /// <summary>
        /// Parses a single pipeline stage string into a ParsedCommand object.
        /// Handles tokenization, redirection, arguments, parameters.
        /// </summary>
        private static ParsedCommand? ParseSinglePipelineStage(string stageInput)
        {
            // Tokenize the current pipeline stage input using the new Regex Tokenizer
            List<Token> tokens = RegexTokenizer.Tokenize(stageInput);
            if (tokens.Count == 0) return null;

            // A command can be a PATH-resolved name or a quoted executable path.
            if (tokens[0].Type != TokenType.Identifier
                && tokens[0].Type != TokenType.StringLiteralDQ
                && tokens[0].Type != TokenType.StringLiteralSQ)
            {
                // Handle error: Expected a command name identifier
                CoreConsole.WriteLine($"ERROR (Parser): Expected command name, but got token type {tokens[0].Type} ('{tokens[0].Value}')");
                return null; // Or throw exception
            }
            string commandName = NormalizeInvocationToken(tokens[0]);
            List<Token> remainingTokens = tokens.Skip(1).ToList(); // Start with tokens after command name

            // Create the command object early so we can add redirections
            var parsedCommand = new ParsedCommand(commandName, new List<object>(), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)); // Arguments list is List<object>

            // --- Advanced Redirection Parsing ---
            // Loop through tokens to find and process *all* redirection operators
            // We need to iterate carefully as we remove items from the list
            for (int i = 0; i < remainingTokens.Count; /* No increment here */)
            {
                Token currentToken = remainingTokens[i];
                bool processed = false; // Flag to indicate if the token was processed as part of a redirection

                if (currentToken.Type == TokenType.Operator)
                {
                    string opValue = currentToken.Value;

                    // Handle Input Redirection '<'
                    if (opValue == "<")
                    {
                        if (i + 1 < remainingTokens.Count)
                        {
                            Token targetToken = remainingTokens[i + 1];
                            if (targetToken.Type == TokenType.Identifier || targetToken.Type == TokenType.StringLiteralDQ || targetToken.Type == TokenType.StringLiteralSQ)
                            {
                                string targetPath = targetToken.Value;
                                // Remove quotes from string literals if present
                                if (targetToken.Type == TokenType.StringLiteralDQ || targetToken.Type == TokenType.StringLiteralSQ)
                                {
                                    if (targetPath.Length >= 2) targetPath = targetPath.Substring(1, targetPath.Length - 2);
                                }

                                if (parsedCommand.InputRedirectPath != null)
                                {
                                    CoreConsole.WriteLine($"WARN (Parser): Multiple input redirections specified. Using last one: '{targetPath}'. Previous was: '{parsedCommand.InputRedirectPath}'.");
                                }
                                parsedCommand.SetInputRedirection(targetPath);
                                
                                remainingTokens.RemoveAt(i + 1); // Remove file path token
                                remainingTokens.RemoveAt(i);     // Remove '<' token
                                processed = true;
                                continue; // Restart check from current index
                            }
                            else
                            {
                                CoreConsole.WriteLine($"WARN (Parser): Input redirection operator '<' found without a valid file path target. Unexpected token type: '{targetToken.Type}'.");
                                remainingTokens.RemoveAt(i); // Remove the invalid '<' operator token
                                processed = true;
                                continue; // Restart check from current index
                            }
                        }
                        else
                        {
                            CoreConsole.WriteLine($"WARN (Parser): Input redirection operator '<' found at the end of command without a target file path.");
                            remainingTokens.RemoveAt(i); // Remove the invalid '<' operator token
                            processed = true;
                            continue; // Restart check from current index
                        }
                    }
                    // Handle Output Redirection (>, >>, 2>, etc.)
                    else if (opValue.Contains(">")) // Check if it's an output redirection operator
                    {
                        ParsedCommand.RedirectionInfo? redirection = TryParseRedirectionOperator(opValue, remainingTokens, i);
                        if (redirection != null)
                        {
                            parsedCommand.AddRedirection(redirection.Value);
                            int tokensToRemove = (redirection.Value.TargetType == ParsedCommand.RedirectionTargetType.StreamHandle) ? 1 : 2;
                            if (tokensToRemove == 2) remainingTokens.RemoveAt(i + 1);
                            remainingTokens.RemoveAt(i);
                            processed = true;
                            continue; // Restart check from current index
                        }
                        else
                        {
                            CoreConsole.WriteLine($"WARN (Parser): Failed to parse output redirection for operator token '{opValue}'.");
                            remainingTokens.RemoveAt(i); // Remove the invalid operator token
                            processed = true;
                            continue; // Restart check from current index
                        }
                    }
                    // Potentially other operators not related to redirection could be here
                }

                // If the token wasn't processed as redirection, move to the next one
                if (!processed)
                {
                    i++;
                }
            }


            // Preserve an ordered argv view for external commands before the
            // built-in parameter binder interprets leading '-' tokens.
            parsedCommand.InvocationArguments.AddRange(ParseInvocationArguments(remainingTokens));

            // --- Argument/Parameter Parsing (using remaining tokens AFTER redirection removal) ---
            List<object> arguments = parsedCommand.Arguments;
            Dictionary<string, string> parameters = parsedCommand.Parameters;
            var currentArgumentBuilder = new StringBuilder(); // Builder for concatenating argument parts

            for (int i = 0; i < remainingTokens.Count; i++)
            {
                Token currentToken = remainingTokens[i];

                // Check for parameter name
                if (currentToken.Type == TokenType.ParameterName)
                {
                    // If we were building an argument, add it before processing the parameter
                    if (currentArgumentBuilder.Length > 0)
                    {
                        arguments.Add(currentArgumentBuilder.ToString());
                        currentArgumentBuilder.Clear();
                    }

                    string paramName = currentToken.Value;
                    string? paramValue = null;

                    // Check if the next token exists and is NOT another parameter name or operator
                    // (Operators should have been handled already, but check just in case)
                    if (i + 1 < remainingTokens.Count &&
                        remainingTokens[i + 1].Type != TokenType.ParameterName &&
                        remainingTokens[i + 1].Type != TokenType.Operator) // Added check for operator
                    {
                        // Value can be Identifier, StringLiteral, Variable, etc.
                        // Expand variable if needed for parameter value
                        Token valueToken = remainingTokens[i + 1];
                        if (valueToken.Type == TokenType.Variable)
                        {
                            string varName = valueToken.Value.Substring(1);
                            paramValue = GetVariableValue(varName);
                            CoreConsole.WriteLine($"DEBUG (Parser): Expanded variable '{valueToken.Value}' to '{paramValue}' for parameter '{paramName}'.");
                        }
                        // TODO: Handle string literal quotes/escapes for parameter values?
                        else
                        {
                            paramValue = valueToken.Value;
                        }
                        i++; // Consume the value token
                    }
                    parameters[paramName] = paramValue ?? string.Empty; // Store even if value is null (for switch parameters)
                }
                // --- SubExpression Parsing ---
                else if (currentToken.Type == TokenType.SubExpressionStart)
                {
                    // If we were building an argument, add it before processing the subexpression
                    if (currentArgumentBuilder.Length > 0)
                    {
                        arguments.Add(currentArgumentBuilder.ToString());
                        currentArgumentBuilder.Clear();
                    }

                    var subExpressionTokens = new List<Token>();
                    int parenNestingLevel = 1;
                    i++; // Move past the '$(' token

                    while (i < remainingTokens.Count)
                    {
                        Token subToken = remainingTokens[i];
                        // Use GroupStart/SubExpressionStart and GroupEnd/SubExpressionEnd for nesting
                        // Use GroupStart/SubExpressionStart and GroupEnd/SubExpressionEnd for nesting
                        if (subToken.Type == TokenType.GroupStart || subToken.Type == TokenType.SubExpressionStart)
                        {
                            parenNestingLevel++;
                        }
                        else if (subToken.Type == TokenType.GroupEnd || subToken.Type == TokenType.SubExpressionEnd)
                        {
                            parenNestingLevel--;
                            if (parenNestingLevel == 0) break; // Found matching end
                        }
                        // Check for the closing parenthesis *before* adding the token
                        if (subToken.Type == TokenType.GroupEnd || subToken.Type == TokenType.SubExpressionEnd)
                        {
                            parenNestingLevel--;
                            if (parenNestingLevel == 0) break; // Found matching end, break loop *before* adding ')'
                        }
                        subExpressionTokens.Add(subToken);
                        i++; // Consume the token added or the token that caused the break
                    }

                    // Check if loop finished because we ran out of tokens before finding the end
                    if (parenNestingLevel != 0)
                    {
                        CoreConsole.WriteLine("WARN (Parser): Unterminated subexpression '$()' found.");
                        // Add collected tokens as a single raw string argument
                        arguments.Add(string.Join("", subExpressionTokens.Select(t => t.Value)));
                    }
                    else
                    {
                        // Successfully parsed subexpression.
                        string subExpressionInput = string.Join(" ", subExpressionTokens.Select(t => t.Value));
                        CoreConsole.WriteLine($"DEBUG (Parser): Recursively parsing subexpression content: '{subExpressionInput}'");
                        List<List<ParsedCommand>> subStatements = Parse(subExpressionInput);

                        if (subStatements.Count > 0)
                        {
                            arguments.Add(subStatements[0]); // Add List<ParsedCommand>
                            CoreConsole.WriteLine($"DEBUG (Parser): Added parsed subexpression (statement 0) as argument.");
                        }
                        else
                        {
                            CoreConsole.WriteLine($"WARN (Parser): Subexpression '$({subExpressionInput})' parsed into zero statements.");
                            arguments.Add(new List<ParsedCommand>());
                        }
                    }
                }
                // --- Type Literal Parsing ---
                else if (currentToken.Type == TokenType.TypeLiteral)
                {
                    // If we were building an argument, add it first
                    if (currentArgumentBuilder.Length > 0)
                    {
                        arguments.Add(currentArgumentBuilder.ToString());
                        currentArgumentBuilder.Clear();
                    }

                    string typeName = currentToken.Value.Substring(1, currentToken.Value.Length - 2).Trim(); // Remove [ and ] and trim
                    arguments.Add($"TypeLiteral:{typeName}"); // Add as a special string argument for now
                    CoreConsole.WriteLine($"DEBUG (Parser): Added TypeLiteral '{typeName}' as argument.");
                }
                else // It's part of a regular argument (Identifier, StringLiteral, Variable, Operator not handled as redirection, etc.)
                {
                    string valueToAppend;
                    // Check if it's a variable token that needs expansion
                    if (currentToken.Type == TokenType.Variable)
                    {
                        string varName = currentToken.Value.Substring(1);
                        valueToAppend = GetVariableValue(varName);
                        CoreConsole.WriteLine($"DEBUG (Parser): Expanding variable '{currentToken.Value}' to '{valueToAppend}' for argument building.");
                    }
                    else if (currentToken.Type == TokenType.StringLiteralDQ)
                    {
                        // Remove surrounding quotes and process escapes
                        string rawString = currentToken.Value.Length >= 2 ? currentToken.Value.Substring(1, currentToken.Value.Length - 2) : "";
                        valueToAppend = ProcessEscapesInString(rawString);
                    }
                    else if (currentToken.Type == TokenType.StringLiteralSQ)
                    {
                        // Remove surrounding quotes, no escape processing for single quotes
                        valueToAppend = currentToken.Value.Length >= 2 ? currentToken.Value.Substring(1, currentToken.Value.Length - 2) : "";
                    }
                    else
                    {
                        // Append other token types' values directly
                        valueToAppend = currentToken.Value;
                    }
                    currentArgumentBuilder.Append(valueToAppend);
                }
            }

            // Add any remaining content in the builder as the last argument
            if (currentArgumentBuilder.Length > 0)
            {
                arguments.Add(currentArgumentBuilder.ToString());
            }

            // Return the command object populated earlier
            return parsedCommand;
        }


        /// <summary>
        /// Attempts to parse a redirection operator token and its potential target.
        /// </summary>
        /// <param name="operatorTokenValue">The string value of the operator token (e.g., "2>", ">&1").</param>
        /// <param name="remainingTokens">The list of remaining tokens after the command name.</param>
        /// <param name="operatorIndex">The index of the operator token in remainingTokens.</param>
        /// <returns>A RedirectionInfo struct if parsing is successful, otherwise null.</returns>
        private static ParsedCommand.RedirectionInfo? TryParseRedirectionOperator(string operatorTokenValue, List<Token> remainingTokens, int operatorIndex)
        {
            int sourceHandle = 1; // Default stdout
            bool append = operatorTokenValue.Contains(">>");
            bool targetIsStream = operatorTokenValue.EndsWith("&");

            // --- Determine Source Handle ---
            string handlePart = "";
            int operatorSymbolIndex = operatorTokenValue.IndexOf('>');
            if (operatorSymbolIndex > 0) // Check if there are digits before '>'
            {
                handlePart = operatorTokenValue.Substring(0, operatorSymbolIndex);
                if (!int.TryParse(handlePart, out sourceHandle))
                {
                    CoreConsole.WriteLine($"WARN (Parser): Invalid source handle '{handlePart}' in redirection operator '{operatorTokenValue}'. Defaulting to 1.");
                    sourceHandle = 1; // Default on parse error
                }
            }
            else
            {
                 sourceHandle = 1; // Default if '>' is the first char
            }
             // Simple override: if it starts with 2 and no other digit, assume stderr
            if (operatorTokenValue.StartsWith("2>") || operatorTokenValue.StartsWith("2>>")) sourceHandle = 2;


            // --- Determine Target ---
            // Note: The Regex patterns in RegexTokenizer should now capture the full operator like "2>&1" or ">&2"
            if (operatorTokenValue.Contains("&")) // Check if it's potentially a stream redirection
            {
                 // Use Regex to extract source (optional) and target handles
                 // Pattern: Optional digits (\d*), followed by > or >>, then & and digits (\d+)
                 Match streamRedirectMatch = Regex.Match(operatorTokenValue, @"^(\d*)>?>(?:&(\d+))$|^(\d*)>(?:&(\d+))$");

                 if (streamRedirectMatch.Success)
                 {
                     // Determine which pattern matched (append >> or overwrite >)
                     string sourceHandleStr = streamRedirectMatch.Groups[1].Success ? streamRedirectMatch.Groups[1].Value : (streamRedirectMatch.Groups[3].Success ? streamRedirectMatch.Groups[3].Value : "");
                     string targetHandleStr = streamRedirectMatch.Groups[2].Success ? streamRedirectMatch.Groups[2].Value : streamRedirectMatch.Groups[4].Value;
                     append = operatorTokenValue.Contains(">>"); // Double check append based on original token

                     // Parse source handle (default to 1 if empty)
                     if (string.IsNullOrEmpty(sourceHandleStr))
                     {
                         sourceHandle = 1;
                     }
                     else if (!int.TryParse(sourceHandleStr, out sourceHandle))
                     {
                         CoreConsole.WriteLine($"WARN (Parser): Invalid source handle '{sourceHandleStr}' in stream redirection '{operatorTokenValue}'. Defaulting to 1.");
                         sourceHandle = 1;
                     }

                     CoreConsole.WriteLine($"DEBUG (ParsedCommand): Added stream redirection: {operatorTokenValue} (Source: {sourceHandle}, Target: {targetHandleStr}, Append: {append})");
                     return new ParsedCommand.RedirectionInfo(sourceHandle, ParsedCommand.RedirectionTargetType.StreamHandle, targetHandleStr, append);
                 }
                 else
                 {
                     // Regex didn't match the expected stream redirection format
                     CoreConsole.WriteLine($"WARN (Parser): Invalid stream redirection operator format '{operatorTokenValue}'. Could not extract target handle.");
                     return null; // Indicate parsing failure
                 }
            }
            else // File target: Expecting next token to be the file path
            {
                if (operatorIndex + 1 < remainingTokens.Count)
                {
                    Token targetToken = remainingTokens[operatorIndex + 1];
                    if (targetToken.Type == TokenType.Identifier || targetToken.Type == TokenType.StringLiteralDQ || targetToken.Type == TokenType.StringLiteralSQ)
                    {
                        // TODO: Handle quotes/escapes in targetToken.Value if necessary
                        string targetPath = targetToken.Value;
                        // Remove quotes from string literals if present
                        if (targetToken.Type == TokenType.StringLiteralDQ || targetToken.Type == TokenType.StringLiteralSQ)
                        {
                             if (targetPath.Length >= 2) // Avoid error on empty strings "" or ''
                                targetPath = targetPath.Substring(1, targetPath.Length - 2);
                        }

                        CoreConsole.WriteLine($"DEBUG (ParsedCommand): Added file redirection: {operatorTokenValue} {targetPath}");
                        return new ParsedCommand.RedirectionInfo(sourceHandle, ParsedCommand.RedirectionTargetType.FilePath, targetPath, append);
                    }
                    else
                    {
                        CoreConsole.WriteLine($"WARN (Parser): Unexpected token type '{targetToken.Type}' used as redirection file path target: '{targetToken.Value}'.");
                        return null; // Indicate parsing failure
                    }
                }
                else
                {
                    CoreConsole.WriteLine($"WARN (Parser): Redirection operator '{operatorTokenValue}' found without a target file path.");
                    return null; // Indicate parsing failure
                }
            }
        }


        // NOTE: Old state machine tokenizer (TokenizeInput, TokenizerStateSM enum, helper methods) removed.

        /// <summary>
        /// Processes escape sequences within a string that was originally double-quoted.
        /// </summary>
        /// <param name="rawString">The raw string content without the surrounding double quotes.</param>
        /// <returns>The string with recognized escape sequences processed.</returns>
        private static string ProcessEscapesInString(string rawString)
        {
            // PowerShell-like escapes: ` (backtick) is the escape character.
            // Common escapes: `` (literal backtick), `0 (null), `a (alert), `b (backspace),
            // `e (escape), `f (form feed), `n (newline), `r (carriage return), `t (tab), `v (vertical tab).
            // Also handles escaping $, ", and \ (though \ is not the primary escape char here,
            // the tokenizer handles \ within DQ strings for compatibility with general expectations).
            // For this shell, we'll focus on common C-style escapes for now, as `\` is used in tokenizer.
            // Let's assume \ is the escape char as per StringLiteralDQ regex: \" \\ \$ etc.
            // The regex `(?:\\.|[^""\\])*` in StringLiteralDQ captures `\\.` for escapes.

            var sb = new StringBuilder();
            for (int i = 0; i < rawString.Length; i++)
            {
                if (rawString[i] == '\\' && i + 1 < rawString.Length)
                {
                    i++; // Move to the character after backslash
                    switch (rawString[i])
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '$': sb.Append('$'); break; // Important for preventing unintended variable expansion if string is re-parsed
                        case '0': sb.Append('\0'); break;
                        case 'a': sb.Append('\a'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'v': sb.Append('\v'); break;
                        // TODO: Add \uXXXX and \xXX unicode/hex escapes if needed
                        default:
                            // Preserve unknown escape sequences so quoted Windows
                            // paths such as C:\Program Files do not lose separators.
                            sb.Append('\\');
                            sb.Append(rawString[i]);
                            break;
                    }
                }
                else
                {
                    sb.Append(rawString[i]);
                }
            }
            return sb.ToString();
        }

        private static List<string> ParseInvocationArguments(List<Token> tokens)
        {
            var arguments = new List<string>();
            var current = new StringBuilder();
            bool argumentStarted = false;
            Token? previous = null;

            foreach (Token token in tokens)
            {
                if (previous is Token previousToken
                    && token.Start > previousToken.End
                    && argumentStarted)
                {
                    arguments.Add(current.ToString());
                    current.Clear();
                    argumentStarted = false;
                }

                current.Append(NormalizeInvocationToken(token));
                argumentStarted = true;
                previous = token;
            }

            if (argumentStarted)
            {
                arguments.Add(current.ToString());
            }

            return arguments;
        }

        private static string NormalizeInvocationToken(Token token)
        {
            if (token.Type == TokenType.StringLiteralDQ)
            {
                string content = token.Value.Length >= 2
                    ? token.Value.Substring(1, token.Value.Length - 2)
                    : string.Empty;
                return ProcessEscapesInString(content);
            }

            if (token.Type == TokenType.StringLiteralSQ)
            {
                return token.Value.Length >= 2
                    ? token.Value.Substring(1, token.Value.Length - 2)
                    : string.Empty;
            }

            if (token.Type == TokenType.Variable)
            {
                return GetVariableValue(token.Value.Substring(1));
            }

            return token.Value;
        }
    }
}


