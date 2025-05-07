// using System.Security.Cryptography;
// using Bencodex.Types;
// using Libplanet.Action;
// using Libplanet.Types;
// using Libplanet.Types.Blocks;
// using Serilog;
// using Serilog.Events;

// namespace Libplanet.Blockchain.Renderers;

// public class LoggedActionRenderer : LoggedRenderer, IActionRenderer
// {
//     public LoggedActionRenderer(
//         IActionRenderer renderer,
//         ILogger logger,
//         LogEventLevel level = LogEventLevel.Debug)
//         : base(renderer, logger, level)
//     {
//         ActionRenderer = renderer;
//     }

//     public IActionRenderer ActionRenderer { get; }

//     public void RenderBlockEnd(Block oldTip, Block newTip)
//         => LogBlockRendering(
//             nameof(RenderBlockEnd),
//             oldTip,
//             newTip,
//             ActionRenderer.RenderBlockEnd);

//     public void RenderAction(IAction action, CommittedActionContext context, HashDigest<SHA256> nextState)
//         => LogActionRendering(
//             nameof(RenderAction),
//             action,
//             context,
//             () => ActionRenderer.RenderAction(action, context, nextState));

//     public void RenderActionError(IAction action, CommittedActionContext context, Exception exception) =>
//         LogActionRendering(
//             nameof(RenderActionError),
//             action,
//             context,
//             () => ActionRenderer.RenderActionError(action, context, exception));

//     private void LogActionRendering(
//         string methodName, IAction action, CommittedActionContext context, System.Action callback)
//     {
//         var actionType = action.GetType();
//         const string startMessage =
//             "Invoking {MethodName}() for an action {ActionType} at block #{BlockHeight}...";
//         Logger.Write(
//             Level,
//             startMessage,
//             methodName,
//             actionType,
//             context.BlockHeight);

//         try
//         {
//             callback();
//         }
//         catch (Exception e)
//         {
//             const string errorMessage =
//                 "An exception was thrown during {MethodName}() for an action {ActionType} at " +
//                 "block #{BlockHeight}";
//             Logger.Error(
//                 e,
//                 errorMessage,
//                 methodName,
//                 actionType,
//                 context.BlockHeight);
//             throw;
//         }

//         const string endMessage =
//             "Invoked {MethodName}() for an action {ActionType} at block #{BlockHeight}";
//         Logger.Write(
//             Level,
//             endMessage,
//             methodName,
//             actionType,
//             context.BlockHeight);
//     }
// }
