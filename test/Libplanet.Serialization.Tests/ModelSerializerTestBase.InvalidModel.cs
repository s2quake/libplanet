// The version must be 1 if there is no ModelHistory.
namespace Libplanet.Serialization.Tests
{
    [Model("Libplanet_Serialization_Tests_InvalidModel_WrongVersion", Version = 3)]
    public sealed record class InvalidModel_WrongVersion
    {
    }

    public abstract partial class ModelSerializerTestBase<TData>
    {
        [Fact]
        public void InvalidModel_WrongVersion_Throw()
        {
            var type = typeof(InvalidModel_WrongVersion);
            var obj = Activator.CreateInstance(type);
            var message = $"The version of type '{type}' must be 1.";
            var e = Assert.Throws<InvalidModelException>(() => Serialize(obj));
            Assert.Equal(message, e.Message);
        }
    }
}

// The version must be one greater than the last ModelHistory version.
namespace Libplanet.Serialization.Tests
{
    [ModelHistory(Version = 1, Type = typeof(Version1))]
    [Model("Libplanet_Serialization_Tests_InvalidModel_WrongVersion_WithHistory", Version = 1)]
    public sealed record class InvalidModel_WrongVersion_WithHistory
    {
        [OriginModel(Type = typeof(InvalidModel_WrongVersion_WithHistory))]
        public sealed record class Version1
        {
            public Version1()
            {
            }
        }
    }

    public abstract partial class ModelSerializerTestBase<TData>
    {
        [Fact]
        public void InvalidModel_WrongVersion_WithHistory_Throw()
        {
            var type = typeof(InvalidModel_WrongVersion_WithHistory);
            var obj = Activator.CreateInstance(type);
            var message = $"The version of type '{type}' must be 2.";
            var e = Assert.Throws<InvalidModelException>(() => Serialize(obj));
            Assert.Equal(message, e.Message);
        }
    }
}

// The constructor that takes the previous version type as a parameter must be defined.
namespace Libplanet.Serialization.Tests
{
    [ModelHistory(Version = 1, Type = typeof(Version1))]
    [ModelHistory(Version = 3, Type = typeof(Version2))]
    [Model("Libplanet_Serialization_Tests_InvalidModel_WrongHistoryVersion", Version = 4)]
    public sealed record class InvalidModel_WrongHistoryVersion
    {
        [OriginModel(Type = typeof(InvalidModel_WrongHistoryVersion))]
        public sealed record class Version1
        {
            public Version1()
            {
            }
        }

        [OriginModel(Type = typeof(InvalidModel_WrongHistoryVersion))]
        public sealed record class Version2
        {
            public Version2()
            {
            }

            public Version2(Version1 version1)
            {
            }
        }

        public InvalidModel_WrongHistoryVersion()
        {
        }

        public InvalidModel_WrongHistoryVersion(Version2 version2)
        {
        }
    }

    public abstract partial class ModelSerializerTestBase<TData>
    {
        [Fact]
        public void InvalidModel_WrongHistoryVersion_Throw()
        {
            var type = typeof(InvalidModel_WrongHistoryVersion);
            var obj = Activator.CreateInstance(type);
            var message = $"The version of type '{typeof(InvalidModel_WrongHistoryVersion.Version2)}' must be 2.";
            var e = Assert.Throws<InvalidModelException>(() => Serialize(obj));
            Assert.Equal(message, e.Message);
        }
    }
}

// The constructor that takes the previous version type as a parameter must be defined.
namespace Libplanet.Serialization.Tests
{
    [ModelHistory(Version = 1, Type = typeof(Version1))]
    [ModelHistory(Version = 2, Type = typeof(Version2))]
    [Model("Libplanet_Serialization_Tests_InvalidModel_ModelHistory_NoConstructor", Version = 3)]
    public sealed record class InvalidModel_ModelHistory_NoConstructor
    {
        [OriginModel(Type = typeof(InvalidModel_ModelHistory_NoConstructor))]
        public sealed record class Version1
        {
        }

        [OriginModel(Type = typeof(InvalidModel_ModelHistory_NoConstructor))]
        public sealed record class Version2
        {
            public Version2()
            {
            }
        }

        public InvalidModel_ModelHistory_NoConstructor()
        {
        }

        public InvalidModel_ModelHistory_NoConstructor(Version2 version2)
        {
        }
    }

    public abstract partial class ModelSerializerTestBase<TData>
    {
        [Fact]
        public void InvalidModel_ModelHistory_NoConstructor_Throw()
        {
            var type = typeof(InvalidModel_ModelHistory_NoConstructor);
            var obj = Activator.CreateInstance(type);
            var message = $"Type '{typeof(InvalidModel_ModelHistory_NoConstructor.Version2)}' does not have a " +
                          $"constructor with a single parameter of type " +
                          $"'{typeof(InvalidModel_ModelHistory_NoConstructor.Version1)}'.";
            var e = Assert.Throws<InvalidModelException>(() => Serialize(obj));
            Assert.Equal(message, e.Message);
        }
    }
}

// The type must have a default constructor.
namespace Libplanet.Serialization.Tests
{
    [ModelHistory(Version = 1, Type = typeof(Version1))]
    [ModelHistory(Version = 2, Type = typeof(Version2))]
    [Model("Libplanet_Serialization_Tests_InvalidModel_ModelHistory_NoDefaultConstructor", Version = 3)]
    public sealed record class InvalidModel_ModelHistory_NoDefaultConstructor
    {
        [OriginModel(Type = typeof(InvalidModel_ModelHistory_NoDefaultConstructor))]
        public sealed record class Version1
        {
        }

        [OriginModel(Type = typeof(InvalidModel_ModelHistory_NoDefaultConstructor))]
        public sealed record class Version2
        {
            public Version2(Version1 version)
            {
            }
        }

        public InvalidModel_ModelHistory_NoDefaultConstructor()
        {
        }

        public InvalidModel_ModelHistory_NoDefaultConstructor(Version2 version2)
        {
        }
    }

    public abstract partial class ModelSerializerTestBase<TData>
    {
        [Fact]
        public void InvalidModel_ModelHistory_NoDefaultConstructor_Throw()
        {
            var type = typeof(InvalidModel_ModelHistory_NoDefaultConstructor);
            var obj = Activator.CreateInstance(type);
            var message = $"Type '{typeof(InvalidModel_ModelHistory_NoDefaultConstructor.Version2)}' does not have a " +
                          "default constructor.";
            var e = Assert.Throws<InvalidModelException>(() => Serialize(obj));
            Assert.Equal(message, e.Message);
        }
    }
}

// The constructor that takes the previous version type as a parameter must be defined.
namespace Libplanet.Serialization.Tests
{
    [ModelHistory(Version = 1, Type = typeof(Version1))]
    [ModelHistory(Version = 2, Type = typeof(Version2))]
    [Model("Libplanet_Serialization_Tests_InvalidModel_NoConstructor", Version = 3)]
    public sealed record class InvalidModel_NoConstructor
    {
        [OriginModel(Type = typeof(InvalidModel_NoConstructor))]
        public sealed record class Version1
        {
        }

        [OriginModel(Type = typeof(InvalidModel_NoConstructor))]
        public sealed record class Version2
        {
            public Version2()
            {
            }

            public Version2(Version1 version1)
            {
            }
        }

        public InvalidModel_NoConstructor()
        {
        }
    }


    public abstract partial class ModelSerializerTestBase<TData>
    {
        [Fact]
        public void InvalidModel_NoConstructor_Throw()
        {
            var type = typeof(InvalidModel_NoConstructor);
            var obj = Activator.CreateInstance(type);
            var message = $"Type '{typeof(InvalidModel_NoConstructor)}' does not have a constructor with " +
                          $"a single parameter of type '{typeof(InvalidModel_NoConstructor.Version2)}'.";
            var e = Assert.Throws<InvalidModelException>(() => Serialize(obj));
            Assert.Equal(message, e.Message);
        }
    }
}

// The type must have a default constructor.
namespace Libplanet.Serialization.Tests
{
    [ModelHistory(Version = 1, Type = typeof(Version1))]
    [ModelHistory(Version = 2, Type = typeof(Version2))]
    [Model("Libplanet_Serialization_Tests_InvalidModel_NoDefaultConstructor", Version = 3)]
    public sealed record class InvalidModel_NoDefaultConstructor
    {
        [OriginModel(Type = typeof(InvalidModel_NoDefaultConstructor))]
        public sealed record class Version1
        {
        }

        [OriginModel(Type = typeof(InvalidModel_NoDefaultConstructor))]
        public sealed record class Version2
        {
            public Version2()
            {
            }

            public Version2(Version1 version1)
            {
            }
        }

        public InvalidModel_NoDefaultConstructor(Version2 version2)
        {
        }
    }

    public abstract partial class ModelSerializerTestBase<TData>
    {
        [Fact]
        public void InvalidModel_NoDefaultConstructor_Throw()
        {
            var type = typeof(InvalidModel_NoDefaultConstructor.Version2);
            var obj = Activator.CreateInstance(type);
            var message = $"Type '{typeof(InvalidModel_NoDefaultConstructor)}' does not have a default constructor.";
            var e = Assert.Throws<InvalidModelException>(() => Serialize(obj));
            Assert.Equal(message, e.Message);
        }
    }
}

// The version of ModelHistory must be consecutive.
namespace Libplanet.Serialization.Tests
{
    [ModelHistory(Version = 1, Type = typeof(Version1))]
    [ModelHistory(Version = 100, Type = typeof(Version2))]
    [Model("Libplanet_Serialization_Tests_InvalidModel_SkippedHistoryVersion", Version = 3)]
    public sealed record class InvalidModel_SkippedHistoryVersion
    {
        public InvalidModel_SkippedHistoryVersion()
        {
        }

        public InvalidModel_SkippedHistoryVersion(Version2 verion2)
        {
        }

        [OriginModel(Type = typeof(InvalidModel_SkippedHistoryVersion))]
        public sealed record class Version1
        {
        }

        [OriginModel(Type = typeof(InvalidModel_SkippedHistoryVersion))]
        public sealed record class Version2
        {
            public Version2()
            {
            }

            public Version2(Version1 verion1)
            {
            }
        }
    }

    public abstract partial class ModelSerializerTestBase<TData>
    {
        [Fact]
        public void InvalidModel_SkippedHistoryVersion_Throw()
        {
            var type = typeof(InvalidModel_SkippedHistoryVersion);
            var obj = Activator.CreateInstance(type);
            var message = $"The version of type '{typeof(InvalidModel_SkippedHistoryVersion.Version2)}' must be 2.";
            var e = Assert.Throws<InvalidModelException>(() => Serialize(obj));
            Assert.Equal(message, e.Message);
        }
    }
}

// The type of ModelHistory must not be duplicated.
namespace Libplanet.Serialization.Tests
{
    [ModelHistory(Version = 1, Type = typeof(Version1))]
    [ModelHistory(Version = 2, Type = typeof(Version1))]
    [Model("Libplanet_Serialization_Tests_InvalidModel_ExistingHistoryType", Version = 3)]
    public sealed record class InvalidModel_ExistingHistoryType
    {
        public InvalidModel_ExistingHistoryType()
        {
        }

        public InvalidModel_ExistingHistoryType(Version1 version1)
        {
        }

        [OriginModel(Type = typeof(InvalidModel_ExistingHistoryType))]
        public sealed record class Version1
        {
            public Version1()
            {
            }

            public Version1(Version1 verion1)
            {
            }
        }
    }

    public abstract partial class ModelSerializerTestBase<TData>
    {
        [Fact]
        public void InvalidModel_ExistingHistoryType_Throw()
        {
            var type = typeof(InvalidModel_ExistingHistoryType);
            var obj = Activator.CreateInstance(type);
            var message = $"Type '{typeof(InvalidModel_ExistingHistoryType.Version1)}' is already defined for " +
                          $"'{typeof(InvalidModel_ExistingHistoryType)}'.";
            var e = Assert.Throws<InvalidModelException>(() => Serialize(obj));
            Assert.Equal(message, e.Message);
        }
    }
}

// The type of ModelHistory must not be duplicated.
namespace Libplanet.Serialization.Tests
{
    public sealed record class InvalidModel_NotModelType
    {
        [Property(0)]
        public int Value { get; init; }
    }

    public abstract partial class ModelSerializerTestBase<TData>
    {
        [Fact]
        public void InvalidModel_NotModelType_Throw()
        {
            var type = typeof(InvalidModel_NotModelType);
            var obj = Activator.CreateInstance(type);
            var message = $"Type '{typeof(InvalidModel_NotModelType)}' is not supported or not registered " +
                          $"in known types.";
            var e = Assert.Throws<InvalidModelException>(() => Serialize(obj));
            Assert.Equal(message, e.Message);
        }
    }
}
