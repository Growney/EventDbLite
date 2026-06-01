namespace EventDbLite.Projections;

public record LiveProjectionRequirement(string? Stream, Type ProjectionType);