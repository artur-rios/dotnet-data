namespace ArturRios.Data.MongoDb;

/// <summary>A <see cref="Document"/> that participates in optimistic concurrency via <see cref="Version"/>.</summary>
public abstract class VersionedDocument : Document
{
    /// <summary>Monotonic version, incremented on each update and checked to detect concurrent writes.</summary>
    public long Version { get; set; }
}
