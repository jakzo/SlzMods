class X {
  public int Main() {
    var chunkLoader =
        SLZ.Marrow.SceneStreaming.SceneStreamer._session.ChunkLoader;
    chunkLoader._activeChunks.Clear();
    foreach (var chunkTrigger in chunkLoader.Triggers)
      chunkLoader.Load(chunkTrigger.chunk);
    foreach (var chunkTrigger in chunkLoader.Triggers)
      chunkLoader._activeChunks.Add(chunkTrigger.chunk);
    foreach (var chunkTrigger in chunkLoader.Triggers)
      chunkLoader.SetOccupiedChunk(chunkTrigger.chunk);
    return SLZ.Marrow.SceneStreaming.SceneStreamer._session.ChunkLoader
        ._occupiedChunks.Count;
    SLZ.Marrow.SceneStreaming.SceneStreamer._session.ChunkLoader.LoadChunks();
  }
}
