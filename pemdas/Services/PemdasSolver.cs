using System.Globalization;
using System.Text.RegularExpressions;
using pemdas.Models;

namespace pemdas.Services;

public sealed class PemdasSolver
{
    private static readonly Regex AllowedInputPattern =
        new(@"^[\d\s.+\-*/^()\[\]{}]+$", RegexOptions.Compiled);

    public static bool IsValidInput(string expression) =>
        AllowedInputPattern.IsMatch(expression);

    public PemdasPageViewModel Solve(string? expression)
    {
        var trimmedExpression = expression?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedExpression))
        {
            return new PemdasPageViewModel
            {
                Expression = string.Empty,
                ErrorMessage = "Enter an expression with up to six numbers.",
            };
        }

        if (!IsValidInput(trimmedExpression))
        {
            return new PemdasPageViewModel
            {
                Expression = trimmedExpression,
                ErrorMessage = "Only numbers, arithmetic operators (+, -, *, /, ^), and grouping symbols ( ) [ ] { } are allowed.",
            };
        }

        try
        {
            var parser = new Parser(trimmedExpression);
            var root = WrapRoot(parser.Parse());

            var steps = new List<PemdasStepViewModel>();
            Evaluate(root, root, steps);
            var finalAnswer = FormatNumber(GetNumericValue(root));

            return new PemdasPageViewModel
            {
                Expression = trimmedExpression,
                FinalAnswer = finalAnswer,
                Steps = steps,
            };
        }
        catch (PemdasException ex)
        {
            return new PemdasPageViewModel
            {
                Expression = trimmedExpression,
                ErrorMessage = ex.Message,
            };
        }
    }

    private static RootNode WrapRoot(ExpressionNode inner)
    {
        var root = new RootNode(inner);
        inner.Parent = root;
        return root;
    }

    private static double Evaluate(ExpressionNode node, ExpressionNode root, ICollection<PemdasStepViewModel> steps)
    {
        switch (node)
        {
            case NumberNode numberNode:
                return numberNode.Value;
            case RootNode rootNode:
                return Evaluate(rootNode.Inner, root, steps);
            case GroupNode groupNode:
                return Evaluate(groupNode.Inner, root, steps);
            case UnaryNode unaryNode:
            {
                Evaluate(unaryNode.Operand, root, steps);

                var before = Render(root, true);
                var operandValue = GetNumericValue(unaryNode.Operand);
                var value = unaryNode.Operator switch
                {
                    '+' => operandValue,
                    '-' => -operandValue,
                    _ => throw new PemdasException("Unsupported unary operator."),
                };

                if (!double.IsFinite(value))
                {
                    throw new PemdasException("The expression produced a number outside the supported range.");
                }

                Replace(unaryNode, value);
                var after = Render(root, true);
                steps.Add(new PemdasStepViewModel
                {
                    Before = before,
                    After = after,
                    Explanation = unaryNode.Operator == '-'
                        ? "Apply the negative sign after solving the grouped value or number it belongs to."
                        : "Apply the positive sign to keep the solved value unchanged.",
                });

                return value;
            }
            case BinaryNode binaryNode:
            {
                Evaluate(binaryNode.Left, root, steps);
                Evaluate(binaryNode.Right, root, steps);

                var leftValue = GetNumericValue(binaryNode.Left);
                var rightValue = GetNumericValue(binaryNode.Right);
                var before = Render(root, true);
                var value = binaryNode.Operator switch
                {
                    '+' => leftValue + rightValue,
                    '-' => leftValue - rightValue,
                    '*' => leftValue * rightValue,
                    '/' when rightValue == 0d => throw new PemdasException("Division by zero is undefined."),
                    '/' => leftValue / rightValue,
                    '^' => Math.Pow(leftValue, rightValue),
                    _ => throw new PemdasException("Unsupported operator."),
                };

                if (!double.IsFinite(value))
                {
                    throw new PemdasException("The expression produced a number outside the supported range.");
                }

                Replace(binaryNode, value);
                var after = Render(root, true);
                steps.Add(new PemdasStepViewModel
                {
                    Before = before,
                    After = after,
                    Explanation = BuildExplanation(binaryNode),
                });

                return value;
            }
            default:
                throw new PemdasException("Unsupported expression.");
        }
    }

    private static void Replace(ExpressionNode node, double value)
    {
        var replacement = new NumberNode(value) { Parent = node.Parent };

        switch (node.Parent)
        {
            case RootNode rootNode:
                rootNode.Inner = replacement;
                break;
            case GroupNode groupNode:
                groupNode.Inner = replacement;
                break;
            case UnaryNode unaryNode:
                unaryNode.Operand = replacement;
                break;
            case BinaryNode binaryNode when ReferenceEquals(binaryNode.Left, node):
                binaryNode.Left = replacement;
                break;
            case BinaryNode binaryNode when ReferenceEquals(binaryNode.Right, node):
                binaryNode.Right = replacement;
                break;
            default:
                throw new PemdasException("Unable to update the expression tree.");
        }
    }

    private static string BuildExplanation(BinaryNode node)
    {
        var insideGrouping = HasGroupingAncestor(node);
        var groupingPrefix = insideGrouping
            ? "Solve the innermost grouping first. "
            : string.Empty;

        return node.Operator switch
        {
            '^' => $"{groupingPrefix}Evaluate exponents before multiplication, division, addition, or subtraction.",
            '*' or '/' => $"{groupingPrefix}After grouping symbols and exponents, solve multiplication and division from left to right.",
            '+' or '-' when insideGrouping => "Solve the innermost grouping first. This addition or subtraction happens now because it is inside grouping symbols.",
            '+' or '-' => "Addition and subtraction come after grouping symbols, exponents, multiplication, and division.",
            _ => "Follow PEMDAS to solve the next operation.",
        };
    }

    private static bool HasGroupingAncestor(ExpressionNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is GroupNode)
            {
                return true;
            }
        }

        return false;
    }

    private static double GetNumericValue(ExpressionNode node) =>
        node switch
        {
            NumberNode numberNode => numberNode.Value,
            RootNode rootNode => GetNumericValue(rootNode.Inner),
            GroupNode groupNode => GetNumericValue(groupNode.Inner),
            _ => throw new PemdasException("The expression could not be fully reduced."),
        };

    private static string Render(ExpressionNode node, bool isRoot = false)
    {
        return node switch
        {
            RootNode rootNode => Render(rootNode.Inner, true),
            NumberNode numberNode => FormatNumber(numberNode.Value),
            UnaryNode unaryNode => $"{unaryNode.Operator}{RenderOperand(unaryNode.Operand)}",
            GroupNode groupNode when isRoot => Render(groupNode.Inner, true),
            GroupNode groupNode => $"{groupNode.OpenSymbol}{Render(groupNode.Inner)}{groupNode.CloseSymbol}",
            BinaryNode binaryNode => $"{RenderBinaryOperand(binaryNode.Left, binaryNode.Operator, true)} {DisplayOperator(binaryNode.Operator)} {RenderBinaryOperand(binaryNode.Right, binaryNode.Operator, false)}",
            _ => string.Empty,
        };
    }

    private static string RenderOperand(ExpressionNode node)
    {
        return node is BinaryNode ? $"({Render(node)})" : Render(node);
    }

    private static string RenderBinaryOperand(ExpressionNode node, char parentOperator, bool isLeftOperand)
    {
        if (node is GroupNode)
        {
            return Render(node);
        }

        if (node is UnaryNode)
        {
            return Render(node);
        }

        if (node is not BinaryNode childBinary)
        {
            return Render(node);
        }

        var parentPrecedence = GetPrecedence(parentOperator);
        var childPrecedence = GetPrecedence(childBinary.Operator);
        var requiresParentheses =
            childPrecedence < parentPrecedence ||
            (!isLeftOperand && parentOperator == '^' && childPrecedence == parentPrecedence) ||
            (!isLeftOperand && (parentOperator == '-' || parentOperator == '/') && childPrecedence == parentPrecedence);

        var rendered = Render(node);
        return requiresParentheses ? $"({rendered})" : rendered;
    }

    private static int GetPrecedence(char op) =>
        op switch
        {
            '+' or '-' => 1,
            '*' or '/' => 2,
            '^' => 3,
            _ => 0,
        };

    private static string DisplayOperator(char op) =>
        op switch
        {
            '*' => "×",
            '/' => "÷",
            '^' => "^",
            _ => op.ToString(),
        };

    private static string FormatNumber(double value)
    {
        if (Math.Abs(value) < 1e-12)
        {
            value = 0d;
        }

        return value.ToString("0.###############", CultureInfo.InvariantCulture);
    }

    private abstract class ExpressionNode
    {
        public ExpressionNode? Parent { get; set; }
    }

    private sealed class RootNode(ExpressionNode inner) : ExpressionNode
    {
        public ExpressionNode Inner { get; set; } = inner;
    }

    private sealed class NumberNode(double value) : ExpressionNode
    {
        public double Value { get; set; } = value;
    }

    private sealed class UnaryNode(char @operator, ExpressionNode operand) : ExpressionNode
    {
        public char Operator { get; } = @operator;

        public ExpressionNode Operand { get; set; } = operand;
    }

    private sealed class BinaryNode(char @operator, ExpressionNode left, ExpressionNode right) : ExpressionNode
    {
        public char Operator { get; } = @operator;

        public ExpressionNode Left { get; set; } = left;

        public ExpressionNode Right { get; set; } = right;
    }

    private sealed class GroupNode(char openSymbol, char closeSymbol, ExpressionNode inner) : ExpressionNode
    {
        public char OpenSymbol { get; } = openSymbol;

        public char CloseSymbol { get; } = closeSymbol;

        public ExpressionNode Inner { get; set; } = inner;
    }

    private sealed class Parser(string expression)
    {
        private readonly IReadOnlyList<Token> _tokens = Tokenize(expression);
        private int _index;

        public ExpressionNode Parse()
        {
            var result = ParseExpression();
            if (_index < _tokens.Count)
            {
                throw new PemdasException($"Unexpected token '{_tokens[_index].Text}'.");
            }

            return result;
        }

        private ExpressionNode ParseExpression()
        {
            var left = ParseTerm();
            while (Match(TokenType.Operator, "+", "-"))
            {
                var op = Previous().Text[0];
                var right = ParseTerm();
                left = Link(new BinaryNode(op, left, right), left, right);
            }

            return left;
        }

        private ExpressionNode ParseTerm()
        {
            var left = ParsePower();
            while (Match(TokenType.Operator, "*", "/"))
            {
                var op = Previous().Text[0];
                var right = ParsePower();
                left = Link(new BinaryNode(op, left, right), left, right);
            }

            return left;
        }

        private ExpressionNode ParsePower()
        {
            var left = ParseUnary();
            if (Match(TokenType.Operator, "^"))
            {
                var right = ParsePower();
                left = Link(new BinaryNode('^', left, right), left, right);
            }

            return left;
        }

        private ExpressionNode ParseUnary()
        {
            if (Match(TokenType.Operator, "+", "-"))
            {
                var op = Previous().Text[0];
                var operand = ParseUnary();
                return Link(new UnaryNode(op, operand), operand);
            }

            return ParsePrimary();
        }

        private ExpressionNode ParsePrimary()
        {
            if (Match(TokenType.Number))
            {
                return new NumberNode(double.Parse(Previous().Text, CultureInfo.InvariantCulture));
            }

            if (Match(TokenType.OpenGroup))
            {
                var open = Previous();
                var inner = ParseExpression();
                var expectedClose = MatchingClose(open.Text[0]).ToString();
                if (!Match(TokenType.CloseGroup, expectedClose))
                {
                    throw new PemdasException($"Expected '{expectedClose}' to close '{open.Text}'.");
                }

                return Link(new GroupNode(open.Text[0], expectedClose[0], inner), inner);
            }

            throw new PemdasException("Expected a number or grouping symbol.");
        }

        private bool Match(TokenType type, params string[] values)
        {
            if (_index >= _tokens.Count || _tokens[_index].Type != type)
            {
                return false;
            }

            if (values.Length > 0 && !values.Contains(_tokens[_index].Text, StringComparer.Ordinal))
            {
                return false;
            }

            _index++;
            return true;
        }

        private Token Previous() => _tokens[_index - 1];

        private static TNode Link<TNode>(TNode parent, params ExpressionNode[] children)
            where TNode : ExpressionNode
        {
            foreach (var child in children)
            {
                child.Parent = parent;
            }

            return parent;
        }

        private static IReadOnlyList<Token> Tokenize(string expression)
        {
            var tokens = new List<Token>();
            var numberCount = 0;

            for (var i = 0; i < expression.Length;)
            {
                var current = expression[i];
                if (char.IsWhiteSpace(current))
                {
                    i++;
                    continue;
                }

                if (char.IsDigit(current) || current == '.')
                {
                    var start = i;
                    var decimalPoints = 0;
                    while (i < expression.Length && (char.IsDigit(expression[i]) || expression[i] == '.'))
                    {
                        if (expression[i] == '.')
                        {
                            decimalPoints++;
                            if (decimalPoints > 1)
                            {
                                throw new PemdasException("Numbers can contain only one decimal point.");
                            }
                        }

                        i++;
                    }

                    var tokenText = expression[start..i];
                    if (tokenText == ".")
                    {
                        throw new PemdasException("A decimal point must belong to a number.");
                    }

                    if (!double.TryParse(tokenText, CultureInfo.InvariantCulture, out _))
                    {
                        throw new PemdasException($"'{tokenText}' is not a valid floating point number.");
                    }

                    tokens.Add(new Token(TokenType.Number, tokenText));
                    numberCount++;
                    if (numberCount > 6)
                    {
                        throw new PemdasException("Use no more than six numbers in one expression.");
                    }

                    continue;
                }

                if ("+-*/^".Contains(current))
                {
                    tokens.Add(new Token(TokenType.Operator, current.ToString()));
                    i++;
                    continue;
                }

                if ("([{".Contains(current))
                {
                    tokens.Add(new Token(TokenType.OpenGroup, current.ToString()));
                    i++;
                    continue;
                }

                if (")]}".Contains(current))
                {
                    tokens.Add(new Token(TokenType.CloseGroup, current.ToString()));
                    i++;
                    continue;
                }

                throw new PemdasException($"Unsupported character '{current}'.");
            }

            if (numberCount == 0)
            {
                throw new PemdasException("Enter an expression with at least one number.");
            }

            return tokens;
        }

        private static char MatchingClose(char openSymbol) =>
            openSymbol switch
            {
                '(' => ')',
                '[' => ']',
                '{' => '}',
                _ => throw new PemdasException("Unsupported grouping symbol."),
            };
    }

    private readonly record struct Token(TokenType Type, string Text);

    private enum TokenType
    {
        Number,
        Operator,
        OpenGroup,
        CloseGroup,
    }

    private sealed class PemdasException(string message) : Exception(message);
}
