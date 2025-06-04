namespace Libplanet.Serialization.Tests;

public sealed partial class ModelSerializerTest
{
    [Theory]
    [InlineData(typeof(InvalidModelRecord1))]
    [InlineData(typeof(InvalidModelRecord2))]
    [InlineData(typeof(InvalidModelRecord3))]
    [InlineData(typeof(InvalidModelRecord4))]
    [InlineData(typeof(InvalidModelRecord5))]
    [InlineData(typeof(InvalidModelRecord6))]
    [InlineData(typeof(InvalidModelRecord7))]
    [InlineData(typeof(InvalidModelRecord8))]
    public void InvalidLegacyModel_SerializeAndDeserialize_ThrowTest(Type type)
    {
        var obj = Activator.CreateInstance(type);
        Assert.Throws<ModelSerializationException>(() => ModelSerializer.SerializeToBytes(obj));
    }

    // Last version cannot have Type property
    [ModelHistory(Version = 1, Type = typeof(ModelRecord))]
    public sealed record class InvalidModelRecord1
    {
    }

    // Last version should be equal to the count of ModelAttribute
    [Model(Version = 3, TypeName = "ModelSerializerTest+InvalidModelRecord2")]
    public sealed record class InvalidModelRecord2
    {
    }

    [ModelHistory(Version = 1, Type = typeof(Version1))]
    [Model(Version = 2, TypeName = "ModelSerializerTest+InvalidModelRecord3")]
    public sealed record class InvalidModelRecord3
    {
        // Previous version should have a LegacyModelAttribute
        public sealed record class Version1
        {
        }
    }

    [ModelHistory(Version = 1, Type = typeof(Version1))]
    [Model(Version = 2, TypeName = "ModelSerializerTest+InvalidModelRecord4")]
    public sealed record class InvalidModelRecord4
    {
        public InvalidModelRecord4()
        {
        }

        public InvalidModelRecord4(Version1 verion1)
        {
        }

        // OriginType should be the same as the type of the last version
        [OriginModel(Type = typeof(InvalidModelRecord3))]
        public sealed record class Version1
        {
        }
    }

    // Each version of the model must be in order.
    [ModelHistory(Version = 1, Type = typeof(Version1))]
    [ModelHistory(Version = 100, Type = typeof(Version2))]
    [Model(Version = 3, TypeName = "ModelSerializerTest+InvalidModelRecord5")]
    public sealed record class InvalidModelRecord5
    {
        public InvalidModelRecord5()
        {
        }

        public InvalidModelRecord5(Version2 verion2)
        {
        }

        [OriginModel(Type = typeof(InvalidModelRecord5))]
        public sealed record class Version1
        {
        }

        [OriginModel(Type = typeof(InvalidModelRecord5))]
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

    // Same type cannot be registered multiple times.
    [ModelHistory(Version = 1, Type = typeof(Version1))]
    [ModelHistory(Version = 2, Type = typeof(Version1))]
    [Model(Version = 3, TypeName = "ModelSerializerTest+InvalidModelRecord6")]
    public sealed record class InvalidModelRecord6
    {
        public InvalidModelRecord6()
        {
        }

        public InvalidModelRecord6(Version2 verion2)
        {
        }

        [OriginModel(Type = typeof(InvalidModelRecord6))]
        public sealed record class Version1
        {
        }

        [OriginModel(Type = typeof(InvalidModelRecord6))]
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

    // The constructor that takes the previous version type as a parameter must be defined.
    [ModelHistory(Version = 1, Type = typeof(Version1))]
    [ModelHistory(Version = 2, Type = typeof(Version2))]
    [Model(Version = 3, TypeName = "ModelSerializerTest+InvalidModelRecord7")]
    public sealed record class InvalidModelRecord7
    {
        [OriginModel(Type = typeof(InvalidModelRecord7))]
        public sealed record class Version1
        {
        }

        [OriginModel(Type = typeof(InvalidModelRecord7))]
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

    // If there is an older version, a default constructor must be defined.
    [ModelHistory(Version = 1, Type = typeof(Version1))]
    [ModelHistory(Version = 2, Type = typeof(Version2))]
    [Model(Version = 3, TypeName = "ModelSerializerTest+InvalidModelRecord8")]
    public sealed record class InvalidModelRecord8
    {
        public InvalidModelRecord8()
        {
        }

        public InvalidModelRecord8(Version2 verion2)
        {
        }

        [OriginModel(Type = typeof(InvalidModelRecord8))]
        public sealed record class Version1
        {
        }

        [OriginModel(Type = typeof(InvalidModelRecord8))]
        public sealed record class Version2
        {
            public Version2(Version1 verion1)
            {
            }
        }
    }
}
