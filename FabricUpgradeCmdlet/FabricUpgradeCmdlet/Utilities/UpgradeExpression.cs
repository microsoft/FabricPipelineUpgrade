// <copyright file="UpgradeExpressions.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace FabricUpgradeCmdlet.Utilities
{

    /// <summary>
    /// This class examines and manipulates the LogicApps Workflow Expressions.
    /// </summary>
    public class UpgradeExpression
    {
        // Where is this expression?
        // Used alerting.
        private string path;

        // The string to examine.
        private readonly string expression;

        // We tokenize the expression in order to inspect and manipulate it.
        private List<JToken> tokens = null;

        protected List<JToken> Tokens()
        {
            return new List<JToken>(this.tokens);
        }

        public UpgradeExpression(
            string path,
            string expression)
        {
            this.path = path;
            this.expression = expression;
        }

        /// <summary>
        /// If the expression contains a token like "dataset().xyz",
        /// and parameter has a symbol that matches,
        /// then replace the token with the parameter value.
        /// </summary>
        /// <param name="parameters">Values that can be used to resolve the parameters.</param>
        public void ApplyParameters(
            ResourceParameters parameters)
        {
           
            // TODO: Deal with Object parameters and Object parameter values.

            this.TokenizeExpression();

            List<JToken> updatedTokens = new List<JToken>();

            if ((this.tokens.Count == 2) &&
                (this.tokens[0].ToString() == "@") &&
                (parameters.ContainsParameterName(this.tokens[1].ToString())))
            {
                JToken standaloneValue = parameters.StandaloneValue(this.tokens[1].ToString());

                if (TokenIsExpressionObject(standaloneValue))
                {
                    // We start with an expression like "@dataset().p1"
                    // p1 is { "type": "Expression", "value": "@pipeline().parameters.x" }
                    // Therefore, parse "@pipeline().parameters.x" and make that my new tokens.
                    // When we Rebuild(), these tokens will turn back into
                    // { "type": "Expression", "value": "@pipeline().parameters.x" }
                    // which is exactly what we want.
                    var newExpression = new UpgradeExpression("", standaloneValue.SelectToken("value")?.ToString());
                    newExpression.TokenizeExpression();
                    updatedTokens = newExpression.Tokens();
                }
                else
                {
                    // We start with an expression like "@dataset().p1"
                    // pq is 'abc'
                    // Therefore, just set our tokens to 'abc'.
                    // When we Rebuild(), this token will turn into 'abc'
                    // which is exactly what we want.
                    updatedTokens.Add(standaloneValue);
                }
            }
            else
            {
                // This is not a "singleton" expression.
                // Instead, it's something like "@concat(dataset().fileName, '.json')"
                foreach (string token in this.tokens)
                {
                    // Go through the tokens one at a time to find the ones that match a parameter.
                    // TODO: Handle a parameter of type Object and a token that starts with the parameter name.
                    // For example, p1 is an Object, and the token is "@dataset().p1.first".
                    if (parameters.ContainsParameterName(token))
                    {
                        JToken standaloneValue = parameters.StandaloneValue(token);

                        if (TokenIsExpressionObject(standaloneValue))
                        {
                            // We start with an expression like "@concat(dataset().p1, '.json')"
                            // p1 is { "type": "Expression", "value": "@concat(pipeline().parameters.x, 'X')" }
                            // Therefore, parse "@concat(pipeline().parameters.x, 'X')" => [ "@", "concat", "(", ...].
                            // We skip the "@" and insert these new tokens into our updated tokens.
                            // When we Rebuild(), these tokens will turn back into
                            // { "type": "Expression", "value": "@concat(concat(pipeline().parameters.x, 'X'), '.json')" }
                            // which is not the most compact form, but still does what we want.

                            var newExpression = new UpgradeExpression("", standaloneValue.SelectToken("value")?.ToString());
                            newExpression.TokenizeExpression();
                            List<JToken> insertTokens = newExpression.Tokens();
                            foreach (string insertToken in insertTokens[1..])
                            {
                                updatedTokens.Add(insertToken);
                            }
                        }
                        else
                        {
                            // Not an expression, so just stick in the matching token.
                            // We start with an expression like "@concat(dataset().p1, '.json')"
                            // p1 is "abc", so its IntegratedValue is "'abc'".
                            // When we Rebuild(), the updated tokens will produce "@concat('abc', '.json')"
                            // which is exactly what we want.
                            updatedTokens.Add(parameters.IntegratedValue(token));
                        }
                    }
                    else
                    {
                        // Not a parameter, so just append this token to the updated tokens.
                        updatedTokens.Add(token);
                    }
                }
            }

            this.tokens = updatedTokens;
        }

        /// <summary>
        /// See if there any invalid components in this expression.
        /// </summary>
        /// <param name="alerts">Add any generated alerts to this collector.</param>
        /// <returns>True if and only if the expression is valid.</returns>
        public bool Validate(
            AlertCollector alerts)
        {
            this.TokenizeExpression();

            bool isValid = true;

            isValid &= this.CheckForInvalidTokens(alerts);
            return isValid;
        }

        /// <summary>
        /// Put our tokens back together to create a new value/expression.
        /// </summary>
        /// <returns>The rebuilt expression.</returns>
        public JToken RebuildExpression()
        {
            if (this.tokens.All(t => t == null))
            {
                return null;
            }

            if (this.tokens.Count == 1)
            {
                return this.tokens[0];
            }

            if (this.tokens[0].ToString() != "@")
            {
                // This is not an Expression, so just stick the token(s) back together.
                // TODO: Can we wind up here? If tokens.Count > 1, will this not start with "@"?
                return string.Join(string.Empty, this.tokens);
            }

            // Our first token is "@", so we build an Expression object.
            JObject newExpression = new JObject();
            newExpression["value"] = string.Join(string.Empty, this.tokens);
            newExpression["type"] = "Expression";
            return newExpression;
        }

        /// <summary>
        /// Compute if an expression references anything that it should not.
        /// </summary>
        /// <param name="expression">The string to examine.</param>
        /// <param name="propertyPath">The 'path' to the property, for any new alerts.</param>
        /// <param name="alerts">Add any generated alerts to this list.</param>
        private bool CheckForInvalidTokens(
            AlertCollector alerts)
        {
            bool isValid = true;
            foreach (string token in this.tokens)
            {
                if (token == null) continue;

                isValid &= this.CheckForReferencesToGlobalParameters(token, alerts);
                isValid &= this.CheckForReferencesToPipelineName(token, alerts);
                //isValid &= this.CheckForReferencesToDatasetParameters(token, alerts);
                //isValid &= this.CheckForReferencesToLinkedServiceParameters(token, alerts);
            }
            return isValid;
        }

        /// <summary>
        /// See if the token contains the sequence 'pipeline().globalParameters'.
        /// </summary>
        /// <param name="token">The token to examine.</param>
        /// <param name="alerts">Add any generated alerts to this list.</param>
        /// <returns>If the expression is valid.</returns>
        private bool CheckForReferencesToGlobalParameters(
            string token,
            AlertCollector alerts)
        {
            if (token.ToLower().StartsWith("pipeline().globalparameters.") ||
                token.ToLower().StartsWith("pipeline()?.globalparameters.") ||
                token.ToLower().StartsWith("pipeline().globalparameters?.") ||
                token.ToLower().StartsWith("pipeline()?.globalparameters?."))
            {
                alerts.AddPermanentError($"{this.path} references GlobalParameters and that is not allowed in an Upgrade");
                return false;
            }
            return true;
        }

        /// <summary>
        /// See if the token contains the sequence 'pipeline().Pipeline'.
        /// </summary>
        /// <remarks>
        /// This expression resolves to the name of the pipeline in ADF,
        /// but resolves to the ID of the pipeline in Fabric.
        /// Since this would cause the upgraded pipeline's behavior to differ
        /// from the original pipeline's behavior, we block this upgrade.
        /// </remarks>
        /// <param name="token">The token to examine.</param>
        /// <param name="alerts">Add any generated alerts to this list.</param>
        /// <returns>If the expression is valid.</returns>
        private bool CheckForReferencesToPipelineName(
            string token,
            AlertCollector alerts)
        {
            // TODO: Allow pipeline().pipeline by converting it to pipeline().pipelineName

            // There are no expressions that contain this string as a substring.
            if (token.ToLower() == "pipeline().pipeline" ||
                token.ToLower() == "pipeline()?.pipeline")
            {
                alerts.AddPermanentError($"{this.path} references pipeline().Pipeline and that is not allowed in an Upgrade");
                return false;
            }
            return true;
        }

        /// <summary>
        /// See if the token contains the sequence 'dataset()'.
        /// </summary>
        /// <param name="token">The token to examine.</param>
        /// <param name="alerts">Add any generated alerts to this list.</param>
        /// <returns>If the expression is valid.</returns>
        private bool CheckForReferencesToDatasetParameters(
            string token,
            AlertCollector alerts)
        {
            if (token.ToLower().StartsWith("dataset().") ||
                token.ToLower().StartsWith("dataset()?."))
            {
                alerts.AddPermanentError($"{this.path} references Dataset Parameters and that is not allowed in an Upgrade");
                return false;
            }
            return true;
        }

        /// <summary>
        /// See if the token contains the sequence 'linkedService()'.
        /// </summary>
        /// <param name="token">The token to examine.</param>
        /// <param name="alerts">Add any generated alerts to this list.</param>
        /// <returns>If the expression is valid.</returns>
        private bool CheckForReferencesToLinkedServiceParameters(
            string token,
            AlertCollector alerts)
        {
            if (token.ToLower().StartsWith("linkedservice().") ||
                token.ToLower().StartsWith("linkedservice()?."))
            {
                alerts.AddPermanentError($"{this.path} references LinkedService Parameters and that is not allowed in an Upgrade");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Facilitate parsing by removing extraneous whitespace.
        /// </summary>
        /// <param name="toStrip">The original string to trim.</param>
        /// <returns></returns>
        private string StripSpacesExceptInStrings(string toStrip)
        {
            string parsing = toStrip ?? string.Empty;
            string stripped = string.Empty;

            while(!string.IsNullOrEmpty(parsing))
            {
                if (this.ConsumeStringConstant(parsing, out string stringConstant, out parsing))
                {
                    stripped += stringConstant;
                }
                else if (" \r\n\t".Contains(parsing[0]))
                {
                    parsing = parsing[1..];
                }
                else
                {
                    stripped += parsing[0];
                    parsing = parsing[1..];
                }
            }

            return stripped;
        }

        /// <summary>
        /// Convert our expression into a list of tokens.
        /// </summary>
        /// <remarks>
        /// There is no reason for this tokenization to match the "real" tokenization.
        /// We only need to tokenize enough for our own operations.
        /// That's why "pipeline().a.b.c" is considered one token.
        /// </remarks>
        private void TokenizeExpression()
        {
            if (this.tokens != null)
            {
                return;
            }

            this.tokens = new List<JToken>();

            string parsing = this.expression?[..] ?? string.Empty;
            parsing = StripSpacesExceptInStrings(parsing);

            if (parsing.StartsWith('@'))
            {
                this.tokens.Add("@");
                parsing = parsing[1..];
            }

            // Regex just _hates_ linefeeds.
            // Therefore, we just split this into separate lines and tokenize each line.
            string[] lines = parsing.Split("\n");

            bool isFirstLine = true;
            foreach (string line in lines)
            {
                if (!isFirstLine)
                {
                    this.tokens.Add("\n");
                }
                isFirstLine = false;

                this.TokenizeOneLine(line);
            }
        }

        /// <summary>
        /// Convert one line into tokens.
        /// </summary>
        /// <param name="line">The line to tokenize.</param>
        private void TokenizeOneLine(string line)
        {
            string parsing = line[..];

            while (!string.IsNullOrEmpty(parsing))
            {
                parsing = this.ConsumeOneToken(parsing, out string token);
                this.tokens.Add(token);
            }
        }

        /// <summary>
        /// Advance by one token, return the token and tail.
        /// </summary>
        /// <param name="parsing">The string we are parsing.</param>
        /// <param name="token">The token.</param>
        /// <returns>The rest of the string.</returns>
        private string ConsumeOneToken(string parsing, out string token)
        {
            token = string.Empty;

            if (this.ConsumeStringConstant(parsing, out token, out parsing))
            {
                return parsing;
            }

            if (this.ConsumeOneOf(parsing, " \r\n,()[]", out token, out parsing))
            {
                return parsing;
            }

            if (this.ConsumeParameter(parsing, out token, out parsing))
            {
                return parsing;
            }

            (token, parsing) = BreakAtFirst(parsing, ",\\(\\)\\[\\]");

            if (parsing.StartsWith("()"))
            {
                token += "()";
                parsing = parsing[2..];
            }

            return parsing;
        }

        /// <summary>
        /// If this is a string, then consume up to the first un-escaped single quote.
        /// </summary>
        /// <param name="parsing">The string we are parsing.</param>
        /// <param name="token">The string (if parsing starts with a single quote)</param>
        /// <param name="tail">The unparsed remainder of the string.</param>
        /// <returns>True if and only if this method consumed a string constant.</returns>
        private bool ConsumeStringConstant(string parsing, out string token, out string tail)
        {
            token = string.Empty;
            tail = parsing;

            if (string.IsNullOrEmpty(parsing) || parsing[0] != '\'')
            {
                return false;
            }

            const char QuoteChar = '\'';
            bool inString = true;
            bool escape = false;
            int stringLength = 0;
            for (; inString; stringLength++)
            {
                char k = parsing[stringLength];
                token += k;
                if (escape)
                {
                    // this is an "escaped" character: the character right after a backslash.
                    // ignore this character, even if it's a quote.
                    escape = false;
                }
                else if (k == '\\')
                {
                    // this is an un-escaped backslash.
                    // the next character is escaped.
                    escape = true;
                }
                else if (stringLength != 0 && k == QuoteChar)
                {
                    // this is an unescaped quote.
                    // the string constant is finished.
                    inString = false;
                }
            }

            tail = parsing[stringLength..];

            return true;
        }

        /// <summary>
        /// Consume a single-character token.
        /// </summary>
        /// <param name="parsing">The string we are parsing.</param>
        /// <param name="oneOfThese">The single-character tokens that may be consumed.</param>
        /// <param name="token">The token, if it is oneOfThese.</param>
        /// <param name="tail">The unparsed remainder of the string.</param>
        /// <returns>True if and only if this method consumed one of these characters.</returns>
        private bool ConsumeOneOf(string parsing, string oneOfThese, out string token, out string tail)
        {
            token = string.Empty;
            tail = parsing;

            if (string.IsNullOrEmpty(parsing) || !oneOfThese.Contains(parsing[0]))
            {
                return false;
            }

            tail = parsing[1..];
            token = parsing[..1];
            if (token == ",")
            {
                token = ", ";
            }
            return true;
        }

        /// <summary>
        /// Consume one parameter; to wit, a string that starts with "pipeline()", "dataset()", or "linkedService()".
        /// </summary>
        /// <param name="parsing">The string we are parsing.</param>
        /// <param name="token">The parameter, if the next token is a parameter.</param>
        /// <param name="tail">The unparsed remainder of the string.</param>
        /// <returns>True if and only if this method consumes a parameter.</returns>
        private bool ConsumeParameter(string parsing, out string token, out string tail)
        {
            token = string.Empty;
            tail = parsing;

            List<string> parameters = ["pipeline", "dataset", "linkedService"];
            foreach (string parameter in parameters)
            {
                if (this.ConsumeSpecificParameter(parsing, parameter, out token, out tail))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Consume a parameter with the specified prefix.
        /// </summary>
        /// <param name="parsing">The string we are parsing.</param>
        /// <param name="prefix">The start of the parameter.</param>
        /// <param name="token">The parameter, if the next token is a parameter.</param>
        /// <param name="tail">The unparsed remainder of the string.</param>
        /// <returns></returns>
        private bool ConsumeSpecificParameter(string parsing, string prefix, out string token, out string tail)
        {
            token = string.Empty;
            tail = parsing;

            if (string.IsNullOrEmpty(parsing))
            {
                return false;
            }

            var parser = new Regex($"^(?'paramset'{prefix}\\(\\)\\.)(?'tail'.*$)");
            var matches = parser.Matches(parsing);
            if (matches.Count == 0)
            {
                // The string did not start with <prefix>().
                return false;
            }

            tail = matches[0].Groups["tail"].Value;

            token += prefix + "().";

            // Consume until we hit whitespace, a comma, or a closed parenthesis.
            (string restOfToken, tail) = BreakAtFirst(tail, " \\r\\n,)");

            // Stick all that together.
            token += restOfToken;
            return true;
        }

        /// <summary>
        /// Split the string at any of the specified characters.
        /// </summary>
        /// <param name="parsing">The string we are parsing.</param>
        /// <param name="breakers">Split at the first instance of any of these characters.</param>
        /// <returns>The string before the breaker; the string including and after the breaker.</returns>

        private static Tuple<string, string> BreakAtFirst(
            string parsing,
            string breakers)
        {
            // Find the token up to the breakers. The rest of the string is the tail.
            var parser = new Regex($"^\\s*(?'token'[^{breakers}]*)(?'tail'.*$)");
            var matches = parser.Matches(parsing);

            if (matches.Count == 0)
            {
                // If no breakers appear in the expression, then we are returning the final token.
                return Tuple.Create(parsing, string.Empty);
            }

            string token = matches[0].Groups["token"].Value;
            string tail = matches[0].Groups["tail"].Value;

            return Tuple.Create(token, tail);
        }

        /// <summary>
        /// Check for the magic Expression object.
        /// </summary>
        /// <param name="token">The token to check.</param>
        /// <returns>True if and only if this is an Expression.</returns>
        private static bool TokenIsExpressionObject(JToken token)
        {
            if ((token == null) || (token.Type != JTokenType.Object))
            {
                return false;
            }

            JObject expressionObject = (JObject)token;
            if ((expressionObject.SelectToken("type")?.ToString() == "Expression") &&
                (expressionObject.ContainsKey("value")))
            {
                return true;
            }

            return false;
        }
    }
}
