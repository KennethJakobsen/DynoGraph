using System.Globalization;

namespace RollerGraph.Core.Adjustments;

/// <summary>
/// Tiny safe arithmetic expression compiler. Supports:
///   - operators: + - * / ^ (right-assoc), unary -
///   - parentheses
///   - identifiers: x, pi, e
///   - functions: abs, sqrt, log, log10, exp, sin, cos, tan, min(a,b), max(a,b), pow(a,b)
///   - numeric literals using '.' as the decimal separator (e.g. 1.5, 0.92)
/// Compiled to a closure of the form <c>Func&lt;double, double&gt;</c> where the parameter
/// is bound to <c>x</c>.
/// </summary>
public static class ExpressionParser
{
    public static Func<double, double> Compile(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ExpressionException("Expression is empty.");

        var tokens = Tokenize(expression);
        var parser = new Parser(tokens, expression);
        var node = parser.ParseExpression();
        parser.ExpectEnd();
        return x => node.Evaluate(x);
    }

    /// <summary>
    /// Validates an expression without producing the compiled delegate.
    /// Returns null on success or an error message on failure.
    /// </summary>
    public static string? Validate(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return null;
        try
        {
            Compile(expression);
            return null;
        }
        catch (ExpressionException ex)
        {
            return ex.Message;
        }
    }

    // ------------- Tokenizer -------------

    private enum TokenType
    {
        Number, Identifier, Plus, Minus, Star, Slash, Caret, LParen, RParen, Comma, End,
    }

    private readonly record struct Token(TokenType Type, string Text, int Position);

    private static List<Token> Tokenize(string input)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < input.Length)
        {
            char c = input[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (char.IsDigit(c) || c == '.')
            {
                int start = i;
                bool dot = c == '.';
                i++;
                while (i < input.Length && (char.IsDigit(input[i]) || (input[i] == '.' && !dot)))
                {
                    if (input[i] == '.') dot = true;
                    i++;
                }
                // Scientific notation: 1e10, 2.5E-3
                if (i < input.Length && (input[i] == 'e' || input[i] == 'E'))
                {
                    i++;
                    if (i < input.Length && (input[i] == '+' || input[i] == '-')) i++;
                    while (i < input.Length && char.IsDigit(input[i])) i++;
                }
                tokens.Add(new Token(TokenType.Number, input[start..i], start));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < input.Length && (char.IsLetterOrDigit(input[i]) || input[i] == '_')) i++;
                tokens.Add(new Token(TokenType.Identifier, input[start..i], start));
                continue;
            }

            switch (c)
            {
                case '+': tokens.Add(new Token(TokenType.Plus, "+", i)); i++; continue;
                case '-': tokens.Add(new Token(TokenType.Minus, "-", i)); i++; continue;
                case '*': tokens.Add(new Token(TokenType.Star, "*", i)); i++; continue;
                case '/': tokens.Add(new Token(TokenType.Slash, "/", i)); i++; continue;
                case '^': tokens.Add(new Token(TokenType.Caret, "^", i)); i++; continue;
                case '(': tokens.Add(new Token(TokenType.LParen, "(", i)); i++; continue;
                case ')': tokens.Add(new Token(TokenType.RParen, ")", i)); i++; continue;
                case ',': tokens.Add(new Token(TokenType.Comma, ",", i)); i++; continue;
                default:
                    throw new ExpressionException($"Unexpected character '{c}' at position {i}.");
            }
        }
        tokens.Add(new Token(TokenType.End, string.Empty, input.Length));
        return tokens;
    }

    // ------------- AST -------------

    private abstract class Node
    {
        public abstract double Evaluate(double x);
    }

    private sealed class NumberNode : Node
    {
        public double Value { get; }
        public NumberNode(double v) { Value = v; }
        public override double Evaluate(double _) => Value;
    }

    private sealed class VariableNode : Node
    {
        public override double Evaluate(double x) => x;
    }

    private sealed class UnaryNode : Node
    {
        public Node Operand { get; }
        public bool Negate { get; }
        public UnaryNode(Node op, bool negate) { Operand = op; Negate = negate; }
        public override double Evaluate(double x) => Negate ? -Operand.Evaluate(x) : Operand.Evaluate(x);
    }

    private sealed class BinaryNode : Node
    {
        public Node Left { get; }
        public Node Right { get; }
        public char Op { get; }
        public BinaryNode(Node l, char op, Node r) { Left = l; Op = op; Right = r; }
        public override double Evaluate(double x)
        {
            var a = Left.Evaluate(x);
            var b = Right.Evaluate(x);
            return Op switch
            {
                '+' => a + b,
                '-' => a - b,
                '*' => a * b,
                '/' => a / b,
                '^' => Math.Pow(a, b),
                _ => throw new ExpressionException($"Unknown operator '{Op}'."),
            };
        }
    }

    private sealed class CallNode : Node
    {
        public string Name { get; }
        public Node[] Args { get; }
        public CallNode(string name, Node[] args) { Name = name; Args = args; }
        public override double Evaluate(double x)
        {
            return Name switch
            {
                "abs" => Math.Abs(Args[0].Evaluate(x)),
                "sqrt" => Math.Sqrt(Args[0].Evaluate(x)),
                "log" => Math.Log(Args[0].Evaluate(x)),
                "log10" => Math.Log10(Args[0].Evaluate(x)),
                "exp" => Math.Exp(Args[0].Evaluate(x)),
                "sin" => Math.Sin(Args[0].Evaluate(x)),
                "cos" => Math.Cos(Args[0].Evaluate(x)),
                "tan" => Math.Tan(Args[0].Evaluate(x)),
                "min" => Math.Min(Args[0].Evaluate(x), Args[1].Evaluate(x)),
                "max" => Math.Max(Args[0].Evaluate(x), Args[1].Evaluate(x)),
                "pow" => Math.Pow(Args[0].Evaluate(x), Args[1].Evaluate(x)),
                _ => throw new ExpressionException($"Unknown function '{Name}'."),
            };
        }
    }

    // ------------- Parser -------------

    private sealed class Parser
    {
        private static readonly HashSet<string> KnownVariables = new(StringComparer.OrdinalIgnoreCase) { "x" };
        private static readonly Dictionary<string, double> Constants = new(StringComparer.OrdinalIgnoreCase)
        {
            ["pi"] = Math.PI,
            ["e"] = Math.E,
        };
        private static readonly Dictionary<string, int> FunctionArities = new(StringComparer.OrdinalIgnoreCase)
        {
            ["abs"] = 1, ["sqrt"] = 1, ["log"] = 1, ["log10"] = 1, ["exp"] = 1,
            ["sin"] = 1, ["cos"] = 1, ["tan"] = 1,
            ["min"] = 2, ["max"] = 2, ["pow"] = 2,
        };

        private readonly List<Token> _tokens;
        private readonly string _source;
        private int _i;

        public Parser(List<Token> tokens, string source) { _tokens = tokens; _source = source; }

        private Token Peek() => _tokens[_i];
        private Token Next() => _tokens[_i++];

        public Node ParseExpression() => ParseAddSub();

        private Node ParseAddSub()
        {
            var left = ParseMulDiv();
            while (Peek().Type is TokenType.Plus or TokenType.Minus)
            {
                var op = Next().Type == TokenType.Plus ? '+' : '-';
                var right = ParseMulDiv();
                left = new BinaryNode(left, op, right);
            }
            return left;
        }

        private Node ParseMulDiv()
        {
            var left = ParseUnary();
            while (Peek().Type is TokenType.Star or TokenType.Slash)
            {
                var op = Next().Type == TokenType.Star ? '*' : '/';
                var right = ParseUnary();
                left = new BinaryNode(left, op, right);
            }
            return left;
        }

        private Node ParseUnary()
        {
            if (Peek().Type == TokenType.Minus)
            {
                Next();
                return new UnaryNode(ParseUnary(), negate: true);
            }
            if (Peek().Type == TokenType.Plus)
            {
                Next();
                return ParseUnary();
            }
            return ParsePower();
        }

        private Node ParsePower()
        {
            var left = ParsePrimary();
            if (Peek().Type == TokenType.Caret)
            {
                Next();
                var right = ParseUnary(); // right-associative
                return new BinaryNode(left, '^', right);
            }
            return left;
        }

        private Node ParsePrimary()
        {
            var t = Next();
            switch (t.Type)
            {
                case TokenType.Number:
                    if (!double.TryParse(t.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                        throw new ExpressionException($"Invalid number '{t.Text}' at {t.Position}.");
                    return new NumberNode(n);

                case TokenType.LParen:
                {
                    var inner = ParseExpression();
                    if (Next().Type != TokenType.RParen)
                        throw new ExpressionException("Missing closing ')'.");
                    return inner;
                }

                case TokenType.Identifier:
                {
                    var name = t.Text;
                    if (Peek().Type == TokenType.LParen)
                    {
                        Next(); // consume (
                        if (!FunctionArities.TryGetValue(name, out var arity))
                            throw new ExpressionException($"Unknown function '{name}' at {t.Position}.");
                        var args = new List<Node>(arity);
                        if (Peek().Type != TokenType.RParen)
                        {
                            args.Add(ParseExpression());
                            while (Peek().Type == TokenType.Comma)
                            {
                                Next();
                                args.Add(ParseExpression());
                            }
                        }
                        if (Next().Type != TokenType.RParen)
                            throw new ExpressionException($"Missing ')' in call to '{name}'.");
                        if (args.Count != arity)
                            throw new ExpressionException($"Function '{name}' expects {arity} argument(s), got {args.Count}.");
                        return new CallNode(name.ToLowerInvariant(), args.ToArray());
                    }

                    if (Constants.TryGetValue(name, out var c))
                        return new NumberNode(c);
                    if (KnownVariables.Contains(name))
                        return new VariableNode();

                    throw new ExpressionException($"Unknown identifier '{name}' at {t.Position}.");
                }

                case TokenType.Minus:
                    return new UnaryNode(ParseUnary(), negate: true);

                default:
                    throw new ExpressionException($"Unexpected token '{t.Text}' at {t.Position}.");
            }
        }

        public void ExpectEnd()
        {
            if (Peek().Type != TokenType.End)
                throw new ExpressionException($"Unexpected trailing token '{Peek().Text}' at {Peek().Position} in '{_source}'.");
        }
    }
}
