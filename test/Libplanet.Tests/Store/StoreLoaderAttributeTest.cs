using System.Collections.Specialized;
#if !NETFRAMEWORK
using static System.Web.HttpUtility;
#endif
using Libplanet.Store;
#if NETFRAMEWORK
using static Mono.Web.HttpUtility;
#endif

namespace Libplanet.Tests.Store
{
    public class StoreLoaderAttributeTest
    {
        [Fact]
        public void Constructor()
        {
            var attr = new StoreLoaderAttribute("Test");
            Assert.Equal("test", attr.UriScheme);
        }

        [Fact]
        public void ListStoreLoaders()
        {
            Assert.Contains(
                StoreLoaderAttribute.ListStoreLoaders(),
                pair => pair.UriScheme == "test" &&
                    pair.DeclaringType == typeof(StoreLoaderAttributeTest));
            Assert.DoesNotContain(
                StoreLoaderAttribute.ListStoreLoaders(),
                pair => pair.UriScheme == "non-existent");
        }

        [Fact]
        public void LoadStore()
        {
            Assert.Null(StoreLoaderAttribute.LoadStore(new Uri("non-existent+test://")));
            (Libplanet.Store.Store Store, TrieStateStore StateStore)? pair =
                StoreLoaderAttribute.LoadStore(new Uri("test:///"));
            Assert.NotNull(pair);
            Assert.IsAssignableFrom<Libplanet.Store.Store>(pair.Value.Store);
            Assert.IsAssignableFrom<TrieStateStore>(pair.Value.StateStore);
        }

        [StoreLoader("test")]
        private static (Libplanet.Store.Store Store, TrieStateStore StateStore) TestLoader(Uri storeUri)
        {
            NameValueCollection query = ParseQueryString(storeUri.Query);
            var store = new Libplanet.Store.Store(new MemoryDatabase());
            var stateStore = new TrieStateStore();
            return (store, stateStore);
        }
    }
}
