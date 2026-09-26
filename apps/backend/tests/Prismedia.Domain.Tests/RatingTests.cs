using Prismedia.Domain.Media;

namespace Prismedia.Domain.Tests;

public sealed class RatingTests {
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(6, 5)]
    public void RateClampsRatingsOutsideTheSharedZeroToFiveScale(int value, int normalizedValue) {
        var video = new Video(Guid.NewGuid(), "Test");

        video.Rate(value);

        Assert.Equal(normalizedValue, video.RatingValue);
    }
}
