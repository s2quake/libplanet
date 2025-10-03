// OriginModelAttribute.Type must equal to the type where ModelHistory is defined.
namespace Libplanet.Serialization.Tests
{
    [ModelHistory(Version = 1, Type = typeof(Version1))]
    [Model("Libplanet_Serialization_Tests_OriginModel_InvalidModelType", Version = 2)]
    public sealed record class OriginModel_InvalidModelType
    {
        [OriginModel(Type = typeof(ModelRecord))]
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
        public void OriginModelAttribute_InvalidModelType_Throw()
        {
            var type = typeof(OriginModel_InvalidModelType);
            var obj = Activator.CreateInstance(type);
            var message = $"{nameof(OriginModelAttribute)}.{nameof(OriginModelAttribute.Type)} of " +
                          $"'{typeof(OriginModel_InvalidModelType.Version1)}' " +
                          $"must be '{typeof(OriginModel_InvalidModelType)}'.";
            var e = Assert.Throws<InvalidModelException>(() => Serialize(obj));
            Assert.Equal(message, e.Message);
        }
    }
}

// OriginModelAttribute.Type must not be object.
namespace Libplanet.Serialization.Tests
{
    [ModelHistory(Version = 1, Type = typeof(Version1))]
    [Model("Libplanet_Serialization_Tests_OriginModel_ObjectType", Version = 2)]
    public sealed record class OriginModel_ObjectType
    {
        [OriginModel(Type = typeof(object))]
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
        public void OriginModelAttribute_ObjectType_Throw()
        {
            var type = typeof(OriginModel_ObjectType);
            var obj = Activator.CreateInstance(type);
            var message = $"{nameof(OriginModelAttribute)}.{nameof(OriginModelAttribute.Type)} of " +
                          $"'{typeof(OriginModel_ObjectType.Version1)}' " +
                          $"must be '{typeof(OriginModel_ObjectType)}'.";
            var e = Assert.Throws<InvalidModelException>(() => Serialize(obj));
            Assert.Equal(message, e.Message);
        }
    }
}

// The type must be a known model type.
namespace Libplanet.Serialization.Tests
{
    public sealed record class OriginModel_UnknownModelType
    {
        [OriginModel(Type = typeof(OriginModel_UnknownModelType))]
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
        public void OriginModelAttribute_UnknownModelType_Throw()
        {
            var type = typeof(OriginModel_UnknownModelType.Version1);
            var obj = Activator.CreateInstance(type);
            var message = $"Type '{typeof(OriginModel_UnknownModelType)}' does not have the " +
                          $"{nameof(ModelAttribute)}.";
            var e = Assert.Throws<InvalidModelException>(() => Serialize(obj));
            Assert.Equal(message, e.Message);
        }
    }
}

// The type must be a known model type.
namespace Libplanet.Serialization.Tests
{
    [ModelHistory(Version = 1, Type = typeof(Version1))]
    [Model("Libplanet_Serialization_Tests_OriginModel_NotDefined", Version = 2)]
    public sealed record class OriginModel_NotDefined
    {
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
        public void OriginModelAttribute_NotDefined_Throw()
        {
            var type = typeof(OriginModel_NotDefined);
            var obj = Activator.CreateInstance(type);
            var message = $"Type '{typeof(OriginModel_NotDefined.Version1)}' does not have the " +
                          $"{nameof(OriginModelAttribute)}.";
            var e = Assert.Throws<InvalidModelException>(() => Serialize(obj));
            Assert.Equal(message, e.Message);
        }
    }
}
