using System.Globalization;

namespace RollerGraph.Core.Adjustments;

/// <summary>
/// Instance-based implementation of <see cref="IExpressionCompiler"/>. The
/// supported function set is determined by the <see cref="IFunctionRegistry"/>
/// supplied at construction time, so new functions can be registered without
/// touching this class (Open/Closed).
///
/// Supports:
///   - operators: + - * / ^ (right-assoc), unary -
///   - parentheses
///   - identifiers: <c>x</c>, <c>pi</c>, <c>e</c>
///   - any function present in the registry
///   - numeric literals using '.' as the decimal separator (e.g. 1.5, 0.92)
///   - scientific notation (1e10, 2.5E-3)
/// </summary>
public sealed class ExpressionCompiler : IExpressionCompiler
{
    private readonly IFunctionRegistry _functions;

    /// <summary>Creates a compiler that recognises the given function set.</summary>
    public ExpressionCompiler(IFunctionRegistry functions)
    {
        ArgumentNullException.ThrowIfNull(functions);
        _functions = functions;
    }

    /// <summary>Creates a compiler seeded with RollerGraph's default math functions.</summary>
    public ExpressionCompiler() : this(FunctionRegistry.StandardMath())
    {
    }

    public Func<double, double> Compile(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ExpressionException("Expression is empty.");

        var tokens = Tokenize(expression);
        var parser = new Parser(tokens, expression, _functions);
        var node = parser.ParseExpression();
        parser.ExpectEnd();
        return x => node.Evaluate(x);
    }

    public string? Validate(string? expression)
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
        private readonly double _value;
        public NumberNode(double v) { _value = v; }
        public override double Evaluate(double _) => _value;
    }

    private sealed class VariableNode : Node
    {
        public override double Evaluate(double x) => x;
    }

    private sealed class UnaryNode : Node
    {
        private readonly Node _operand;
        private readonly bool _negate;
        public UnaryNode(Node op, bool negate) { _operand = op; _negate = negate; }
        public override double Evaluate(double x) => _negate ? -_operand.Evaluate(x) : _operand.Evaluate(x);
    }

    private sealed class BinaryNode : Node
    {
        private readonly Node _left;
        private readonly Node _right;
        private readonly char _op;
        public BinaryNode(Node l, char op, Node r) { _left = l; _op = op; _right = r; }
        public override double Evaluate(double x)
        {
            var a = _left.Evaluate(x);
            var b = _right.Evaluate(x);
            return _op switch
            {
                '+' => a + b,
                '-' => a - b,
                '*' => a * b,
                '/' => a / b,
                '^' => Math.Pow(a, b),
                _ => throw new ExpressionException($"Unknown operator '{_op}'."),
            };
        }
    }

    private sealed class CallNode : Node
    {
        private readonly string _name;
        private readonly Node[] _args;
        private readonly IFunctionRegistry _functions;

        public CallNode(string name, Node[] args, IFunctionRegistry functions)
        {
            _name = name;
            _args = args;
            _functions = functions;
        }

        public override double Evaluate(double x)
        {
            // Evaluate arguments into a stack-allocated buffer when small,
            // falling back to a heap allocation for large arities. This keeps
            // dispatch allocation-free for the common 1- and 2-arg functions.
            if (_args.Length <= 8)
            {
                Span<double> buffer = stackalloc double[_args.Length];
                for (int i = 0; i < _args.Length; i++) buffer[i] = _args[i].Evaluate(x);
                return _functions.Invoke(_name, buffer);
            }
            else
            {
                var arr = new double[_args.Length];
                for (int i = 0; i < _args.Length; i++) arr[i] = _args[i].Evaluate(x);
                return _functions.Invoke(_name, arr);
            }
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

        private readonly List<Token> _tokens;
        private readonly string _source;
        private readonly IFunctionRegistry _functions;
        private int _i;

        public Parser(List<Token> tokens, string source, IFunctionRegistry functions)
        {
            _tokens = tokens;
            _source = source;
            _functions = functions;
        }

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
                        if (!_functions.IsFunction(name))
                            throw new ExpressionException($"Unknown function '{name}' at {t.Position}.");
                        var arity = _functions.GetArity(name);
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
                        return new CallNode(name.ToLowerInvariant(), args.ToArray(), _functions);
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
