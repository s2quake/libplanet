using System.Security.Cryptography;
using GraphQL;
using GraphQL.Types;
using Libplanet.State;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Types;

namespace Libplanet.Explorer.Queries;

public class StateQuery : ObjectGraphType<Blockchain>
{
    public StateQuery()
    {
        Name = "StateQuery";

        Field<NonNullGraphType<WorldStateType>>(
            "world",
            arguments: new QueryArguments(
                new QueryArgument<BlockHashType> { Name = "blockHash" },
                new QueryArgument<HashDigestType<SHA256>> { Name = "stateRootHash" }),
            resolve: ResolveWorldState);

        Field<NonNullGraphType<ListGraphType<BencodexValueType>>>(
            "states",
            description: "Retrieves states from the legacy account.",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<ListGraphType<NonNullGraphType<AddressType>>>>
                    { Name = "addresses" },
                new QueryArgument<IdGraphType> { Name = "offsetBlockHash" },
                new QueryArgument<HashDigestSHA256Type> { Name = "offsetStateRootHash" }),
            resolve: ResolveStates);
        Field<NonNullGraphType<FungibleAssetValueType>>(
            "balance",
            description: "Retrieves balance from the legacy account.",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<AddressType>> { Name = "owner" },
                new QueryArgument<NonNullGraphType<CurrencyInputType>> { Name = "currency" },
                new QueryArgument<IdGraphType> { Name = "offsetBlockHash" },
                new QueryArgument<HashDigestSHA256Type> { Name = "offsetStateRootHash" }),
            resolve: ResolveBalance);
        Field<FungibleAssetValueType>(
            "totalSupply",
            description: "Retrieves total supply from the legacy account.",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<CurrencyInputType>> { Name = "currency" },
                new QueryArgument<IdGraphType> { Name = "offsetBlockHash" },
                new QueryArgument<HashDigestSHA256Type> { Name = "offsetStateRootHash" }),
            resolve: ResolveTotalSupply);
        Field<ListGraphType<NonNullGraphType<ValidatorType>>>(
            "validators",
            description: "Retrieves validator set from the legacy account.",
            arguments: new QueryArguments(
                new QueryArgument<IdGraphType> { Name = "offsetBlockHash" },
                new QueryArgument<HashDigestSHA256Type> { Name = "offsetStateRootHash" }),
            resolve: ResolveValidatorSet);
    }

    private static object ResolveWorldState(IResolveFieldContext<Blockchain> context)
    {
        BlockHash? blockHash = context.GetArgument<BlockHash?>("blockHash");
        HashDigest<SHA256>? stateRootHash =
            context.GetArgument<HashDigest<SHA256>?>("stateRootHash");

        switch (blockhash: blockHash, srh: stateRootHash)
        {
            case (blockhash: not null, srh: not null):
                throw new ExecutionError(
                    "blockHash and stateRootHash cannot be specified at the same time.");
            case (blockhash: null, srh: null):
                throw new ExecutionError(
                    "Either blockHash or stateRootHash must be specified.");
            case (blockhash: not null, _):
                return context.Source.GetWorld((BlockHash)blockHash);
            case (_, srh: not null):
                return context.Source.GetWorld(stateRootHash ?? default);
        }
    }

    private static object? ResolveStates(IResolveFieldContext<Blockchain> context)
    {
        Address[] addresses = context.GetArgument<Address[]>("addresses");
        BlockHash? offsetBlockHash =
            context.GetArgument<string?>("offsetBlockHash") is { } blockHashString
                ? BlockHash.Parse(blockHashString)
                : null;
        HashDigest<SHA256>? offsetStateRootHash = context
            .GetArgument<HashDigest<SHA256>?>("offsetStateRootHash");

        switch (blockhash: offsetBlockHash, srh: offsetStateRootHash)
        {
            case (blockhash: not null, srh: not null):
                throw new ExecutionError(
                    "offsetBlockHash and offsetStateRootHash cannot be specified at the same time.");
            case (blockhash: null, srh: null):
                throw new ExecutionError(
                    "Either offsetBlockHash or offsetStateRootHash must be specified.");
            case (blockhash: not null, _):
            {
                var world = context.Source.GetWorld((BlockHash)offsetBlockHash);
                return addresses.Select(address =>
                    world.GetAccount(SystemAddresses.SystemAccount)
                        .GetValue(address))
                .ToArray();
            }

            case (_, srh: not null):
            {
                var world = context.Source.GetWorld(offsetStateRootHash ?? default);
                return addresses.Select(address =>
                    world.GetAccount(SystemAddresses.SystemAccount)
                        .GetValue(address))
                .ToArray();
            }
        }
    }

    private static object ResolveBalance(IResolveFieldContext<Blockchain> context)
    {
        Address owner = context.GetArgument<Address>("owner");
        Currency currency = context.GetArgument<Currency>("currency");
        BlockHash? offsetBlockHash =
            context.GetArgument<string?>("offsetBlockHash") is { } blockHashString
                ? BlockHash.Parse(blockHashString)
                : null;
        HashDigest<SHA256>? offsetStateRootHash = context
            .GetArgument<HashDigest<SHA256>?>("offsetStateRootHash");

        switch (blockhash: offsetBlockHash, srh: offsetStateRootHash)
        {
            case (blockhash: not null, srh: not null):
                throw new ExecutionError(
                    "offsetBlockHash and offsetStateRootHash cannot be specified at the same time.");
            case (blockhash: null, srh: null):
                throw new ExecutionError(
                    "Either offsetBlockHash or offsetStateRootHash must be specified.");
            case (blockhash: not null, _):
            {
                return context.Source
                    .GetWorld((BlockHash)offsetBlockHash)
                    .GetBalance(owner, currency);
            }

            case (_, srh: not null):
                return context.Source
                    .GetWorld(offsetStateRootHash ?? default)
                    .GetBalance(owner, currency);
        }
    }

    private static object? ResolveTotalSupply(IResolveFieldContext<Blockchain> context)
    {
        Currency currency = context.GetArgument<Currency>("currency");
        BlockHash? offsetBlockHash =
            context.GetArgument<string?>("offsetBlockHash") is { } blockHashString
                ? BlockHash.Parse(blockHashString)
                : null;
        HashDigest<SHA256>? offsetStateRootHash = context
            .GetArgument<HashDigest<SHA256>?>("offsetStateRootHash");

        switch (blockhash: offsetBlockHash, srh: offsetStateRootHash)
        {
            case (blockhash: not null, srh: not null):
                throw new ExecutionError(
                    "offsetBlockHash and offsetStateRootHash cannot be specified at the same time.");
            case (blockhash: null, srh: null):
                throw new ExecutionError(
                    "Either offsetBlockHash or offsetStateRootHash must be specified.");
            case (blockhash: not null, _):
                return context.Source
                    .GetWorld((BlockHash)offsetBlockHash)
                    .GetTotalSupply(currency);
            case (_, srh: not null):
                return context.Source
                    .GetWorld(offsetStateRootHash ?? default)
                    .GetTotalSupply(currency);
        }
    }

    private static object? ResolveValidatorSet(IResolveFieldContext<Blockchain> context)
    {
        BlockHash? offsetBlockHash =
            context.GetArgument<string?>("offsetBlockHash") is { } blockHashString
                ? BlockHash.Parse(blockHashString)
                : null;
        HashDigest<SHA256>? offsetStateRootHash = context
            .GetArgument<HashDigest<SHA256>?>("offsetStateRootHash");

        switch (blockhash: offsetBlockHash, srh: offsetStateRootHash)
        {
            case (blockhash: not null, srh: not null):
                throw new ExecutionError(
                    "offsetBlockHash and offsetStateRootHash cannot be specified at the same time.");
            case (blockhash: null, srh: null):
                throw new ExecutionError(
                    "Either offsetBlockHash or offsetStateRootHash must be specified.");
            case (blockhash: not null, _):
                return context.Source
                    .GetWorld((BlockHash)offsetBlockHash)
                    .GetValidators();
            case (_, srh: not null):
                return context.Source
                    .GetWorld(offsetStateRootHash ?? default)
                    .GetValidators();
        }
    }
}
