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
        Assert.Throws<ModelSerializationException>(() => ModelSerializer.Serialize(obj));
    }

    // Last version cannot have Type property
    [Model(Version = 1, Type = typeof(ModelRecord))]
    public sealed record class InvalidModelRecord1
    {
    }

    // Last version should be equal to the count of ModelAttribute
    [Model(Version = 3)]
    public sealed record class InvalidModelRecord2
    {
    }

    [Model(Version = 1, Type = typeof(Version1))]
    [Model(Version = 2)]
    public sealed record class InvalidModelRecord3
    {
        // Previous version should have a LegacyModelAttribute
        public sealed record class Version1
        {
        }
    }

    [Model(Version = 1, Type = typeof(Version1))]
    [Model(Version = 2)]
    public sealed record class InvalidModelRecord4
    {
        public InvalidModelRecord4()
        {
        }

        public InvalidModelRecord4(Version1 verion1)
        {
        }

        // OriginType should be the same as the type of the last version
        [LegacyModel(OriginType = typeof(InvalidModelRecord3))]
        public sealed record class Version1
        {
        }
    }

    // Each version of the model must be in order.
    [Model(Version = 1, Type = typeof(Version1))]
    [Model(Version = 100, Type = typeof(Version2))]
    [Model(Version = 3)]
    public sealed record class InvalidModelRecord5
    {
        public InvalidModelRecord5()
        {
        }

        public InvalidModelRecord5(Version2 verion2)
        {
        }

        [LegacyModel(OriginType = typeof(InvalidModelRecord5))]
        public sealed record class Version1
        {
        }

        [LegacyModel(OriginType = typeof(InvalidModelRecord5))]
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
    [Model(Version = 1, Type = typeof(Version1))]
    [Model(Version = 2, Type = typeof(Version1))]
    [Model(Version = 3)]
    public sealed record class InvalidModelRecord6
    {
        public InvalidModelRecord6()
        {
        }

        public InvalidModelRecord6(Version2 verion2)
        {
        }

        [LegacyModel(OriginType = typeof(InvalidModelRecord6))]
        public sealed record class Version1
        {
        }

        [LegacyModel(OriginType = typeof(InvalidModelRecord6))]
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
    [Model(Version = 1, Type = typeof(Version1))]
    [Model(Version = 2, Type = typeof(Version2))]
    [Model(Version = 3)]
    public sealed record class InvalidModelRecord7
    {
        [LegacyModel(OriginType = typeof(InvalidModelRecord7))]
        public sealed record class Version1
        {
        }

        [LegacyModel(OriginType = typeof(InvalidModelRecord7))]
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
    [Model(Version = 1, Type = typeof(Version1))]
    [Model(Version = 2, Type = typeof(Version2))]
    [Model(Version = 3)]
    public sealed record class InvalidModelRecord8
    {
        public InvalidModelRecord8()
        {
        }

        public InvalidModelRecord8(Version2 verion2)
        {
        }

        [LegacyModel(OriginType = typeof(InvalidModelRecord8))]
        public sealed record class Version1
        {
        }

        [LegacyModel(OriginType = typeof(InvalidModelRecord8))]
        public sealed record class Version2
        {
            public Version2(Version1 verion1)
            {
            }
        }
    }
}
