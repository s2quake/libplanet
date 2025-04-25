// namespace Libplanet.Types.Blocks
// {
//     /// <summary>
//     /// The extension methods for <see cref="BlockExcerpt"/>.
//     /// </summary>
//     public static class BlockExcerptExtensions
//     {
//         /// <summary>
//         /// Shows <see cref="BlockExcerpt"/> instance's members as a string.
//         /// </summary>
//         /// <param name="excerpt">An excerpt object to show.</param>
//         /// <returns>Extracted members as a string.</returns>
//         public static string ToExcerptString(this BlockExcerpt excerpt)
//         {
//             return
//                 $"{excerpt.GetType().Name} {{" +
//                 $" {nameof(excerpt.ProtocolVersion)} = {excerpt.ProtocolVersion}," +
//                 $" {nameof(excerpt.Index)} = {excerpt.Index}," +
//                 $" {nameof(excerpt.Hash)} = {excerpt.Hash}," +
//                 " }";
//         }

//         public static bool ExcerptEquals(this BlockExcerpt excerpt, BlockExcerpt other)
//         {
//             return excerpt.ProtocolVersion.Equals(other.ProtocolVersion)
//                 && excerpt.Index.Equals(other.Index)
//                 && excerpt.Hash.Equals(other.Hash);
//         }
//     }
// }
