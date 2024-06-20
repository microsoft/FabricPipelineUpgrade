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
        // Used for logging and alerting.
        private string path;

        private readonly string expression;

        private List<JToken> tokens = null;

        public List<JToken> Tokens()
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
                    var newUE = new UpgradeExpression("", standaloneValue.SelectToken("value")?.ToString());
                    newUE.TokenizeExpression();
                    updatedTokens = newUE.Tokens();
                }
                else
                {
                    updatedTokens.Add(standaloneValue);
                }
            }
            else
            {
                // "@concat(dataset().fileName, '.json')" => "@concat('otter', '.json')".
                foreach (string token in this.tokens)
                {
                    if (parameters.ContainsParameterName(token))
                    {
                        JToken standaloneValue = parameters.StandaloneValue(token);

                        if (TokenIsExpressionObject(standaloneValue))
                        {
                            var newUE = new UpgradeExpression("", standaloneValue.SelectToken("value")?.ToString());
                            newUE.TokenizeExpression();
                            List<JToken> insertTokens = newUE.Tokens();
                            foreach (string insertToken in insertTokens[1..])
                            {
                                updatedTokens.Add(insertToken);
                            }
                        }
                        else
                        {
                            updatedTokens.Add(parameters.IntegratedValue(token));
                        }
                    }
                    else
                    {
                        updatedTokens.Add(token);
                    }
                }
            }

            this.tokens = updatedTokens;
        }

        public bool Validate(
            AlertCollector alerts)
        {
            this.TokenizeExpression();

            bool isValid = true;

            isValid &= this.CheckForInvalidParameterReferences(alerts);
            return isValid;
        }


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
                return string.Join(string.Empty, this.tokens);
            }

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
        private bool CheckForInvalidParameterReferences(
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

        private string StripSpacesExceptInStrings(string toStrip)
        {
            string parsing = toStrip ?? string.Empty;
            string stripped = string.Empty;

            while(!string.IsNullOrEmpty(parsing))
            {
                if (this.ConsumeString(parsing, out string stringConstant, out parsing))
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

        private void TokenizeOneLine(string line)
        {
            string parsing = line[..];

            while (!string.IsNullOrEmpty(parsing))
            {
                parsing = this.ConsumeOneToken(parsing, out string token);
                this.tokens.Add(token);
            }
        }

        private string ConsumeOneToken(string parsing, out string token)
        {
            token = string.Empty;

            if (this.ConsumeString(parsing, out token, out parsing))
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

        private bool ConsumeString(string parsing, out string token, out string tail)
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
                return false;
            }

            tail = matches[0].Groups["tail"].Value;

            // We'll allow stripping the whitespace from, e.g., 'dataset ()'
            token += prefix + "().";

            (string restOfToken, tail) = BreakAtFirst(tail, " \\r\\n,)");

            token += restOfToken;
            return true;
        }

        private static Tuple<string, string> BreakAtFirst(
            string expression,
            string breakers)
        {
            // Find the token up to the breakers. The rest of the string is the tail.
            var parser = new Regex($"^\\s*(?'token'[^{breakers}]*)(?'tail'.*$)");
            var matches = parser.Matches(expression);

            if (matches.Count == 0)
            {
                // If no breakers appear in the expression, then we are returning the final token.
                return Tuple.Create(expression, string.Empty);
            }

            string token = matches[0].Groups["token"].Value;
            string tail = matches[0].Groups["tail"].Value;

            return Tuple.Create(token, tail);
        }

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
