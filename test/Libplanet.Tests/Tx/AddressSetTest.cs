using System.Collections.Generic;
using System.Collections.Immutable;
using Libplanet.Crypto;
using Libplanet.Types.Tx;
using Xunit;

namespace Libplanet.Tests.Tx
{
    public class AddressSetTest
    {
        [Fact]
        public void Empty()
        {
            Assert.Empty(AddressSet.Empty);
        }

        [Fact]
        public void Constructor()
        {
            Address[] addresses =
            [
                Address.Parse("4000000000000000000000000000000000000000"),
                Address.Parse("3000000000000000000000000000000000000001"),
                Address.Parse("2000000000000000000000000000000000000002"),
                Address.Parse("1000000000000000000000000000000000000003"),
                Address.Parse("0000000000000000000000000000000000000004"),

                // dups:
                Address.Parse("0000000000000000000000000000000000000004"),
                Address.Parse("2000000000000000000000000000000000000002"),
                Address.Parse("4000000000000000000000000000000000000000"),
            ];
            var set = new AddressSet(addresses);
            Assert.Equal(5, set.Count);
            Assert.Equal<IEnumerable<Address>>(
                [
                    Address.Parse("4000000000000000000000000000000000000000"),
                    Address.Parse("3000000000000000000000000000000000000001"),
                    Address.Parse("2000000000000000000000000000000000000002"),
                    Address.Parse("1000000000000000000000000000000000000003"),
                    Address.Parse("0000000000000000000000000000000000000004"),
                ],
                set
            );
        }

        [Fact]
        public void TryGetValue()
        {
            var set = new AddressSet(
            [
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("1000000000000000000000000000000000000001"),
                Address.Parse("0000000000000000000000000000000000000002"),
            ]);

            bool found = set.TryGetValue(
                Address.Parse("1000000000000000000000000000000000000001"),
                out Address value);
            Assert.True(found);
            Assert.Equal(Address.Parse("1000000000000000000000000000000000000001"), value);

            found = set.TryGetValue(
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
                out Address value2);
            Assert.False(found);
            Assert.Equal(default(Address), value2);
        }

        [Fact]
        public void SymmetricExcept()
        {
            var set = new AddressSet(
            [
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("1000000000000000000000000000000000000001"),
                Address.Parse("0000000000000000000000000000000000000002"),
            ]);

            IImmutableSet<Address> result = set.SymmetricExcept(
            [
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
                default(Address),
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("1000000000000000000000000000000000000001"),
                default(Address),
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
            ]);
            Assert.Equal<IEnumerable<Address>>(
                [
                    Address.Parse("2000000000000000000000000000000000000000"),
                    Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
                    default(Address),
                ],
                result
            );
        }

        [Fact]
        public void IsProperSubsetOf()
        {
            var set = new AddressSet(
            [
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("1000000000000000000000000000000000000001"),
                Address.Parse("0000000000000000000000000000000000000002"),
            ]);

            bool result = set.IsProperSubsetOf(
            [
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
                Address.Parse("1000000000000000000000000000000000000001"),
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
            ]);
            Assert.True(result);

            result = set.IsProperSubsetOf(
            [
                Address.Parse("1000000000000000000000000000000000000001"),
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("1000000000000000000000000000000000000001"),
            ]);
            Assert.False(result);

            result = set.IsProperSubsetOf(
            [
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("0000000000000000000000000000000000000002"),
            ]);
            Assert.False(result);

            result = set.IsProperSubsetOf(
            [
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
            ]);
            Assert.False(result);
        }

        [Fact]
        public void IsProperSupersetOf()
        {
            var set = new AddressSet(
            [
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("1000000000000000000000000000000000000001"),
                Address.Parse("0000000000000000000000000000000000000002"),
            ]);

            bool result = set.IsProperSupersetOf(
            [
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("2000000000000000000000000000000000000000"),
            ]);
            Assert.True(result);

            result = set.IsProperSupersetOf(
            [
                Address.Parse("1000000000000000000000000000000000000001"),
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("1000000000000000000000000000000000000001"),
            ]);
            Assert.False(result);

            result = set.IsProperSupersetOf(
            [
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
            ]);
            Assert.False(result);

            result = set.IsProperSupersetOf(
            [
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
                Address.Parse("1000000000000000000000000000000000000001"),
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
            ]);
            Assert.False(result);
        }

        [Fact]
        public void IsSubsetOf()
        {
            var set = new AddressSet(
            [
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("1000000000000000000000000000000000000001"),
                Address.Parse("0000000000000000000000000000000000000002"),
            ]);

            bool result = set.IsSubsetOf(
            [
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
                Address.Parse("1000000000000000000000000000000000000001"),
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("1000000000000000000000000000000000000001"),
            ]);
            Assert.True(result);

            result = set.IsSubsetOf(
            [
                Address.Parse("1000000000000000000000000000000000000001"),
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("1000000000000000000000000000000000000001"),
            ]);
            Assert.True(result);

            result = set.IsSubsetOf(
            [
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("0000000000000000000000000000000000000002"),
            ]);
            Assert.False(result);

            result = set.IsSubsetOf(
            [
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
            ]);
            Assert.False(result);
        }

        [Fact]
        public void IsSupersetOf()
        {
            var set = new AddressSet(
            [
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("1000000000000000000000000000000000000001"),
                Address.Parse("0000000000000000000000000000000000000002"),
            ]);

            bool result = set.IsSupersetOf(
            [
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("0000000000000000000000000000000000000002"),
            ]);
            Assert.True(result);

            result = set.IsSupersetOf(
            [
                Address.Parse("1000000000000000000000000000000000000001"),
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("0000000000000000000000000000000000000002"),
            ]);
            Assert.True(result);

            result = set.IsSupersetOf(
            [
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("2000000000000000000000000000000000000000"),
            ]);
            Assert.False(result);

            result = set.IsSupersetOf(
            [
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
                Address.Parse("1000000000000000000000000000000000000001"),
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("2000000000000000000000000000000000000000"),
            ]);
            Assert.False(result);
        }

        [Fact]
        public void Overlaps()
        {
            var set = new AddressSet(
            [
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("1000000000000000000000000000000000000001"),
                Address.Parse("0000000000000000000000000000000000000002"),
            ]);

            bool result = set.Overlaps(
            [
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
                Address.Parse("1000000000000000000000000000000000000001"),
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("2000000000000000000000000000000000000000"),
                default(Address),
            ]);
            Assert.True(result);

            result = set.Overlaps(
            [
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
                default(Address),
                default(Address),
            ]);
            Assert.False(result);
        }

        [Fact]
        public void Equality()
        {
            var set = new AddressSet(
            [
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("1000000000000000000000000000000000000001"),
                Address.Parse("0000000000000000000000000000000000000002"),
            ]);
            var set2 = new AddressSet(
            [
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("1000000000000000000000000000000000000001"),
            ]);
            var set3 = new AddressSet(
            [
                Address.Parse("1000000000000000000000000000000000000001"),
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
                Address.Parse("0000000000000000000000000000000000000002"),
            ]);
            var set4 = new AddressSet(
            [
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("1000000000000000000000000000000000000001"),
            ]);
            var set5 = new AddressSet(
            [
                Address.Parse("2000000000000000000000000000000000000000"),
                Address.Parse("1000000000000000000000000000000000000001"),
                Address.Parse("0000000000000000000000000000000000000002"),
                Address.Parse("ffffffffffffffffffffffffffffffffffffffff"),
            ]);
            (AddressSet A, AddressSet B, bool Equal)[] truthTable =
            [
                (set, set, true),
                (set, set2, true),
                (set, set3, false),
                (set, set4, false),
                (set, set5, false),
                (set2, set3, false),
                (set2, set4, false),
                (set2, set5, false),
            ];
            foreach ((AddressSet a, AddressSet b, bool equal) in truthTable)
            {
                if (equal)
                {
                    Assert.Equal<AddressSet>(a, b);
                    Assert.Equal<AddressSet>(b, a);
                    Assert.True(a.Equals((object)b));
                    Assert.True(b.Equals((object)a));
                    Assert.True(a.SetEquals(b));
                    Assert.True(b.SetEquals(a));
                    Assert.Equal(a.GetHashCode(), b.GetHashCode());
                }
                else
                {
                    Assert.NotEqual<AddressSet>(a, b);
                    Assert.NotEqual<AddressSet>(b, a);
                    Assert.False(a.Equals((object)b));
                    Assert.False(b.Equals((object)a));
                    Assert.False(a.SetEquals(b));
                    Assert.False(b.SetEquals(a));
                    Assert.NotEqual(b.GetHashCode(), a.GetHashCode());
                }

                Assert.False(a.Equals((AddressSet)null));
                Assert.False(b.Equals((AddressSet)null));
                Assert.False(a.Equals((object)null));
                Assert.False(b.Equals((object)null));
            }
        }
    }
}
