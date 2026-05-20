using System.ComponentModel;
using Kpett.ChatApp.Helper;

namespace Kpett.ChatApp.Tests.Helpers;

public class EnumHelperTests
{
    [Fact]
    public void GetEnumNames_ReturnsAllEnumMemberNames()
    {
        var names = EnumHelper.GetEnumNames<SampleStatus>();

        Assert.Equal(["Pending", "Active", "Archived"], names);
    }

    [Fact]
    public void GetEnumValues_ReturnsUnderlyingIntValues()
    {
        var values = EnumHelper.GetEnumValues<SampleStatus>();

        Assert.Equal([1, 2, 5], values);
    }

    [Theory]
    [InlineData(SampleStatus.Pending, "Waiting")]
    [InlineData(SampleStatus.Active, "Active")]
    public void GetEnumDescription_ReturnsDescriptionOrName(SampleStatus value, string expected)
    {
        var description = EnumHelper.GetEnumDescription(value);

        Assert.Equal(expected, description);
    }

    [Fact]
    public void GetEnumDescriptions_ReturnsOnlyMembersWithDescriptionAttribute()
    {
        var descriptions = EnumHelper.GetEnumDescriptions<SampleStatus>();

        Assert.Equal(["Waiting", "Hidden"], descriptions);
    }

    [Fact]
    public void FromInt_ReturnsEnumValue_WhenDefined()
    {
        var value = EnumHelper.FromInt<SampleStatus>(5);

        Assert.Equal(SampleStatus.Archived, value);
    }

    [Fact]
    public void FromInt_ThrowsArgumentException_WhenValueIsUndefined()
    {
        var exception = Assert.Throws<ArgumentException>(() => EnumHelper.FromInt<SampleStatus>(99));

        Assert.Contains("Value 99 is not defined", exception.Message);
    }

    public enum SampleStatus
    {
        [Description("Waiting")]
        Pending = 1,

        Active = 2,

        [Description("Hidden")]
        Archived = 5
    }
}
