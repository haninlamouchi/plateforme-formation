using PlateformeFormation.Api.Services;

namespace PlateformeFormation.Api.Tests;

// Covers the cosine-similarity scoring at the heart of the RAG retrieval step. The DB-backed
// SearchAsync path itself isn't covered here (needs an EF Core test double), but the scoring math
// it depends on for ranking/filtering (minScore threshold) is otherwise completely untested.
public class RetrievalServiceTests
{
    [Fact]
    public void CosineSimilarity_ReturnsOne_ForIdenticalVectors()
    {
        float[] v = [1f, 2f, 3f];

        Assert.Equal(1f, RetrievalService.CosineSimilarity(v, v), precision: 5);
    }

    [Fact]
    public void CosineSimilarity_ReturnsZero_ForOrthogonalVectors()
    {
        float[] a = [1f, 0f];
        float[] b = [0f, 1f];

        Assert.Equal(0f, RetrievalService.CosineSimilarity(a, b), precision: 5);
    }

    [Fact]
    public void CosineSimilarity_ReturnsNegativeOne_ForOppositeVectors()
    {
        float[] a = [1f, 2f, 3f];
        float[] b = [-1f, -2f, -3f];

        Assert.Equal(-1f, RetrievalService.CosineSimilarity(a, b), precision: 5);
    }

    [Fact]
    public void CosineSimilarity_IsInvariantToMagnitude()
    {
        float[] a = [1f, 2f, 3f];
        float[] scaled = [2f, 4f, 6f];

        Assert.Equal(1f, RetrievalService.CosineSimilarity(a, scaled), precision: 5);
    }

    [Fact]
    public void CosineSimilarity_ReturnsZero_WhenEitherVectorIsZero()
    {
        float[] zero = [0f, 0f, 0f];
        float[] other = [1f, 2f, 3f];

        Assert.Equal(0f, RetrievalService.CosineSimilarity(zero, other));
        Assert.Equal(0f, RetrievalService.CosineSimilarity(other, zero));
    }

    [Fact]
    public void CosineSimilarity_Throws_OnDimensionMismatch()
    {
        float[] a = [1f, 2f, 3f];
        float[] b = [1f, 2f];

        Assert.Throws<InvalidOperationException>(() => RetrievalService.CosineSimilarity(a, b));
    }
}
