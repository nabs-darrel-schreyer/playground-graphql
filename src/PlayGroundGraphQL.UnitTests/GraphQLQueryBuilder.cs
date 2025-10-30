using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace PlayGroundGraphQL.UnitTests;

public class GraphQLQueryBuilder
{
    private readonly StringBuilder _sb = new();
    private readonly Dictionary<string, object> _variables = [];
    private int _variableCounter = 0;

    private GraphQLQueryBuilder(string operation)
    {
        _sb.Append(operation).Append(" { }");
    }

    public static GraphQLQueryBuilder Query() => new("query");
    public static GraphQLQueryBuilder Mutation() => new("mutation");

    // -----------------------------------------------------------------
    // Entity - Single item query with simple arguments (e.g., product(id: 999))
    // -----------------------------------------------------------------
    public GraphQLQueryBuilder Entity<T>(
        string fieldName,
        Expression<Func<T, object>> selector,
        Expression<Func<T, bool>>? where = null)
    {
        var args = new List<string>();

        if (where != null)
        {
            // Extract simple equality arguments (e.g., id == 999 → id: 999)
            var simpleArgs = ExtractSimpleArguments(where);
            args.AddRange(simpleArgs);
        }

        var argPart = args.Any() ? $"({string.Join(", ", args)})" : "";
        var selection = BuildSelectionSet(selector);
        var full = $" {{ {fieldName}{argPart} {{ {selection} }} }}";

        return ReplacePlaceholder(full);
    }

    // -----------------------------------------------------------------
    // Collection with filter & order
    // -----------------------------------------------------------------
    public GraphQLQueryBuilder Collection<T>(
        string fieldName,
        Expression<Func<T, object>> selector,
        Expression<Func<T, bool>>? where = null,
        Expression<Func<T, object>>? order = null,
        bool descending = false)
    {
        var args = new List<string>();

        if (where != null)
            args.Add($"where: {BuildFilter(where)}");

        if (order != null)
        {
            var orderField = BuildOrderBy(order);
            var direction = descending ? "DESC" : "ASC";
            args.Add($"order: {{ {orderField}: {direction} }}");
        }

        var argPart = args.Any() ? $"({string.Join(" ", args)})" : "";
        var selection = BuildSelectionSet(selector);
        var full = $" {{ {fieldName}{argPart} {{ nodes {{ {selection} }} }} }}";

        return ReplacePlaceholder(full);
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------
    private GraphQLQueryBuilder ReplacePlaceholder(string replacement)
    {
        var placeholder = " { }";
        var idx = _sb.ToString().IndexOf(placeholder, StringComparison.Ordinal);
        if (idx == -1) throw new InvalidOperationException("No placeholder left to replace.");

        _sb.Remove(idx, placeholder.Length);
        _sb.Insert(idx, replacement);
        return this;
    }

    private static string BuildSelectionSet(LambdaExpression selector)
    {
        return selector.Body switch
        {
            NewExpression ne => string.Join(" ", ne.Members!.Select(ToGraphQLName)),
            MemberInitExpression mi => string.Join(" ", mi.Bindings.OfType<MemberAssignment>()
                                                            .Select(b => ToGraphQLName(b.Member))),
            _ => throw new NotSupportedException($"Expression type {selector.Body.NodeType} not supported.")
        };
    }

    private static string ToGraphQLName(MemberInfo member)
        => char.ToLowerInvariant(member.Name[0]) + member.Name.Substring(1);

    // -----------------------------------------------------------------
    // Extract simple arguments for Entity queries (e.g., id == 999 → "id: 999")
    // -----------------------------------------------------------------
    private List<string> ExtractSimpleArguments<T>(Expression<Func<T, bool>> filter)
    {
        var args = new List<string>();
        
        if (filter.Body is BinaryExpression { NodeType: ExpressionType.Equal } be)
        {
            var prop = GetMemberName(be.Left);
            var (value, variables) = GetConstantValue(be.Right);
            
            foreach (var (name, val) in variables)
                _variables[name] = val;

            var formattedValue = value is string varName && varName.StartsWith("var")
                ? $"${varName}"
                : FormatValue(value);
                
            args.Add($"{prop}: {formattedValue}");
        }
        else if (filter.Body is UnaryExpression { Operand: BinaryExpression innerBe })
        {
            // Handle nullable types
            return ExtractSimpleArguments(Expression.Lambda<Func<T, bool>>(innerBe, filter.Parameters));
        }

        return args;
    }

    // -----------------------------------------------------------------
    // FILTER: Expression<Func<T, bool>> → { price: { gte: 5 } }
    // -----------------------------------------------------------------
    private string BuildFilter<T>(Expression<Func<T, bool>> filter)
    {
        var (filterObj, usedVariables) = ExpressionToFilterObject(filter.Body);
        foreach (var (name, value) in usedVariables)
            _variables[name] = value;

        return ObjectToGraphQL(filterObj);
    }

    private (Dictionary<string, object>, List<(string, object)>) ExpressionToFilterObject(Expression expr)
    {
        return expr switch
        {
            BinaryExpression
            {
                NodeType: ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual
                                     or ExpressionType.LessThan or ExpressionType.LessThanOrEqual
                                     or ExpressionType.Equal
            } be => HandleComparison(be),
            UnaryExpression { Operand: BinaryExpression be } ue => HandleComparison(be), // for nullable
            _ => throw new NotSupportedException($"Filter expression '{expr}' not supported.")
        };
    }

    private (Dictionary<string, object>, List<(string, object)>) HandleComparison(BinaryExpression be)
    {
        var (left, right) = (be.Left, be.Right);
        var prop = GetMemberName(left);
        var (value, variables) = GetConstantValue(right);

        var op = be.NodeType switch
        {
            ExpressionType.GreaterThan => "gt",
            ExpressionType.GreaterThanOrEqual => "gte",
            ExpressionType.LessThan => "lt",
            ExpressionType.LessThanOrEqual => "lte",
            ExpressionType.Equal => "eq",
            _ => throw new NotSupportedException()
        };

        var filterObj = new Dictionary<string, object>
        {
            [prop] = new Dictionary<string, object> { [op] = value }
        };

        return (filterObj, variables);
    }

    // -----------------------------------------------------------------
    // ORDER: Expression<Func<T, object>> → "price"
    // -----------------------------------------------------------------
    private string BuildOrderBy<T>(Expression<Func<T, object>> order)
    {
        var member = order.Body switch
        {
            MemberExpression mae => mae.Member,
            UnaryExpression { Operand: MemberExpression mae } => mae.Member,
            NewExpression ne => ne.Members?[0] ?? throw new NotSupportedException("OrderBy anonymous type must have at least one property."),
            _ => throw new NotSupportedException("OrderBy must be a property.")
        };

        return ToGraphQLName(member);
    }

    // -----------------------------------------------------------------
    // Helpers: Extract property name & constant value
    // -----------------------------------------------------------------
    private static string GetMemberName(Expression expr)
    {
        return expr switch
        {
            MemberExpression me => ToGraphQLName(me.Member),
            UnaryExpression { Operand: MemberExpression me } => ToGraphQLName(me.Member),
            _ => throw new NotSupportedException()
        };
    }

    private (object value, List<(string name, object value)> variables) GetConstantValue(Expression expr)
    {
        var value = Expression.Lambda(expr).Compile().DynamicInvoke();
        if (value == null) throw new InvalidOperationException("Filter values cannot be null.");

        // If value is not primitive, make it a variable
        if (value is not (int or float or double or string or bool or decimal))
        {
            var varName = $"var{++_variableCounter}";
            return (varName, new List<(string, object)> { (varName, value) });
        }

        return (value, new());
    }

    private static string ObjectToGraphQL(Dictionary<string, object> obj)
    {
        var parts = obj.Select(kvp =>
        {
            var value = kvp.Value switch
            {
                Dictionary<string, object> sub => ObjectToGraphQL(sub),
                string s when s.StartsWith("var") => $"${s}",
                _ => FormatValue(kvp.Value)
            };
            return $"{kvp.Key}: {value}";
        });
        return $"{{ {string.Join(" ", parts)} }}";
    }

    private static string FormatValue(object value)
    {
        return value switch
        {
            string s => $"\"{s}\"",
            bool b => b.ToString().ToLowerInvariant(),
            _ => value.ToString()!
        };
    }

    // -----------------------------------------------------------------
    // Build
    // -----------------------------------------------------------------
    public (string Query, IReadOnlyDictionary<string, object> Variables) Build()
    {
        var final = _sb.ToString().Replace("{}", "").Trim();
        return (final, _variables);
    }

    public override string ToString() => Build().Query;
}
