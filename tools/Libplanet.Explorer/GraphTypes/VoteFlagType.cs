using GraphQL.Language.AST;
using GraphQL.Types;
using Libplanet.Types;

namespace Libplanet.Explorer.GraphTypes
{
    public class VoteTypeType : StringGraphType
    {
        public VoteTypeType()
        {
            Name = "VoteType";
        }

        public override object? Serialize(object? value)
        {
            if (value is Types.VoteType flag)
            {
                switch (flag)
                {
                    case Types.VoteType.Null:
                        return "Null";
                    case Types.VoteType.PreVote:
                        return "PreVote";
                    case Types.VoteType.PreCommit:
                        return "PreCommit";
                    case Types.VoteType.Unknown:
                        return "Unknown";
                }
            }

            throw new ArgumentException($"Expected a voteflag, but {value}", nameof(value));
        }

        public override object? ParseValue(object? value)
        {
            if (value is string flag)
            {
                switch (flag)
                {
                    case "Null":
                        return Types.VoteType.Null;
                    case "PreVote":
                        return Types.VoteType.PreVote;
                    case "PreCommit":
                        return Types.VoteType.PreCommit;
                    case "Unknown":
                        return Types.VoteType.Unknown;
                }
            }
            else if (value is null)
            {
                return null;
            }

            throw new ArgumentException(
                $"Expected a voteflag string but {value}", nameof(value));
        }

        public override object? ParseLiteral(IValue value)
        {
            if (value is StringValue)
            {
                return ParseValue(value.Value);
            }

            return null;
        }
    }
}
