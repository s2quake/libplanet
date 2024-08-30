using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Blockchain.Renderers;
using Libplanet.Common;
using Libplanet.Types.Blocks;
using Microsoft.Extensions.Logging;

namespace Libplanet.Node.Services;

internal sealed class RendererService(
    SynchronizationContext synchronizationContext,
    ILogger<RendererService> logger) : IRendererService, IActionRenderer, IDisposable
{
    private readonly Observable<RenderBlockInfo> _renderBlock = new();
    private readonly Observable<RenderActionInfo> _renderAction = new();
    private readonly Observable<RenderActionErrorInfo> _renderActionError = new();
    private readonly Observable<RenderBlockInfo> _renderBlockEnd = new();

    IObservable<RenderBlockInfo> IRendererService.RenderBlock => _renderBlock;

    IObservable<RenderActionInfo> IRendererService.RenderAction => _renderAction;

    IObservable<RenderActionErrorInfo> IRendererService.RenderActionError => _renderActionError;

    IObservable<RenderBlockInfo> IRendererService.RenderBlockEnd => _renderBlockEnd;

    public void Dispose()
    {
        _renderBlock.Dispose();
        _renderAction.Dispose();
        _renderActionError.Dispose();
        _renderBlockEnd.Dispose();
    }

    void IActionRenderer.RenderAction(
        IValue action, ICommittedActionContext context, HashDigest<SHA256> nextState)
    {
        synchronizationContext.Post(
            state =>
            {
                _renderAction.Invoke(new(action, context, nextState));
                logger.LogDebug(
                    "Rendered an action: {Action} {Context} {NextState}",
                    action,
                    context,
                    nextState);
            },
            null);
    }

    void IActionRenderer.RenderActionError(
        IValue action, ICommittedActionContext context, Exception exception)
    {
        synchronizationContext.Post(
            state =>
            {
                _renderActionError.Invoke(new(action, context, exception));
                logger.LogError(
                    exception,
                    "Failed to render an action: {Action} {Context}",
                    action,
                    context);
            },
            null);
    }

    void IRenderer.RenderBlock(Block oldTip, Block newTip)
    {
        synchronizationContext.Post(
            state =>
            {
                _renderBlock.Invoke(new(oldTip, newTip));
                logger.LogDebug(
                    "Rendered a block: {OldTip} {NewTip}",
                    oldTip,
                    newTip);
            },
            null);
    }

    void IActionRenderer.RenderBlockEnd(Block oldTip, Block newTip)
    {
        synchronizationContext.Post(
            state =>
            {
                _renderBlockEnd.Invoke(new(oldTip, newTip));
                logger.LogDebug(
                    "Rendered a block end: {OldTip} {NewTip}",
                    oldTip,
                    newTip);
            },
            null);
    }
}
