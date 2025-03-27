using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Blockchain.Renderers;
using Libplanet.Common;
using Libplanet.Types.Blocks;
using Microsoft.Extensions.Logging;
using R3;

namespace Libplanet.Node.Services;

internal sealed class RendererService : IRendererService, IActionRenderer, IAsyncDisposable
{
    private readonly Subject<RenderBlockInfo> _renderBlock = new();
    private readonly Subject<RenderActionInfo> _renderAction = new();
    private readonly Subject<RenderActionErrorInfo> _renderActionError = new();
    private readonly Subject<RenderBlockInfo> _renderBlockEnd = new();
    private readonly ILogger<RendererService> _logger;
    private readonly ActionQueue _renderBlockQueue = new();
    private readonly ActionQueue _renderActionQueue = new();
    private readonly ActionQueue _renderActionErrorQueue = new();
    private readonly ActionQueue _renderBlockEndQueue = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private IObservable<RenderBlockInfo>? _renderBlockObservable;
    private IObservable<RenderActionInfo>? _renderActionObservable;
    private IObservable<RenderActionErrorInfo>? _renderActionErrorObservable;
    private IObservable<RenderBlockInfo>? _renderBlockEndObservable;

    public RendererService(ILogger<RendererService> logger)
    {
        _logger = logger;
        _renderBlockObservable = _renderBlock.AsSystemObservable();
        _renderActionObservable = _renderAction.AsSystemObservable();
        _renderActionErrorObservable = _renderActionError.AsSystemObservable();
        _renderBlockEndObservable = _renderBlock.AsSystemObservable();
        _ = _renderBlockQueue.RunAsync(_cancellationTokenSource.Token);
        _ = _renderActionQueue.RunAsync(_cancellationTokenSource.Token);
        _ = _renderActionErrorQueue.RunAsync(_cancellationTokenSource.Token);
        _ = _renderBlockEndQueue.RunAsync(_cancellationTokenSource.Token);
    }

    IObservable<RenderBlockInfo> IRendererService.RenderBlock
        => _renderBlockObservable ??= _renderBlock.AsSystemObservable();

    IObservable<RenderActionInfo> IRendererService.RenderAction
        => _renderActionObservable ??= _renderAction.AsSystemObservable();

    IObservable<RenderActionErrorInfo> IRendererService.RenderActionError
        => _renderActionErrorObservable ??= _renderActionError.AsSystemObservable();

    IObservable<RenderBlockInfo> IRendererService.RenderBlockEnd
        => _renderBlockEndObservable ??= _renderBlock.AsSystemObservable();

    public async ValueTask DisposeAsync()
    {
        await _cancellationTokenSource.CancelAsync();
        _renderBlock.Dispose();
        _renderAction.Dispose();
        _renderActionError.Dispose();
        _renderBlockEnd.Dispose();
        await _renderBlockQueue.DisposeAsync();
        await _renderActionQueue.DisposeAsync();
        await _renderActionErrorQueue.DisposeAsync();
        await _renderBlockEndQueue.DisposeAsync();
        _cancellationTokenSource.Dispose();
    }

    void IActionRenderer.RenderAction(
        IValue action, ICommittedActionContext context, HashDigest<SHA256> nextState)
    {
        _renderActionQueue.Add(() =>
        {
            _renderAction.OnNext(new(action, context, nextState));
            _logger.LogDebug(
                "Rendered an action: {Action} {Context} {NextState}",
                action,
                context,
                nextState);
        });
    }

    void IActionRenderer.RenderActionError(
        IValue action, ICommittedActionContext context, Exception exception)
    {
        _renderActionErrorQueue.Add(() =>
        {
            _renderActionError.OnNext(new(action, context, exception));
            _logger.LogError(
                exception,
                "Failed to render an action: {Action} {Context}",
                action,
                context);
        });
    }

    void IRenderer.RenderBlock(Block oldTip, Block newTip)
    {
        _renderBlockQueue.Add(() =>
        {
            _renderBlock.OnNext(new(oldTip, newTip));
            _logger.LogDebug(
                "Rendered a block: {OldTip} {NewTip}",
                oldTip,
                newTip);
        });
    }

    void IActionRenderer.RenderBlockEnd(Block oldTip, Block newTip)
    {
        _renderBlockEndQueue.Add(() =>
        {
            _renderBlockEnd.OnNext(new(oldTip, newTip));
            _logger.LogDebug(
                "Rendered a block end: {OldTip} {NewTip}",
                oldTip,
                newTip);
        });
    }
}
