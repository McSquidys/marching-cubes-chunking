using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// loads and unloads chunks given ids
/// also loads and unloads collision meshes
/// every frame it gets player position updates via player position
/// <br></br>
/// chunk manager will call these various functions
/// </summary>
public static class ChunkLoader {
    /// <summary>
    /// load new chunk into memory
    /// </summary>
    public static void memLoadCreatedChunk(Int64 id) {

    }

    /// <summary>
    /// load chunk into memory from storage
    /// </summary>
    public static void memLoadChunk() {

    }

    /// <summary>
    /// unload chunk from memory, (queue free or what not)
    /// save state to disk if modification were made
    /// </summary>
    public static void memUnloadChunk() {

    }

    /// <summary>
    /// unload chunk from scene tree
    /// </summary>
    public static void unloadChunk() {

    }

    /// <summary>
    /// load collisions for chunks that are visible (chunk is already in the scene tree)
    /// </summary>
    public static void loadCollision(Int64 id, Dictionary<Int64, Chunk> idToChunk, HashSet<Int64> idToCol) {

        // if adding was successful (returns false if duplicates exist)
        if (idToCol.Add(id)) {
            // if collision needs to be added then
            Chunk chunk = idToChunk[id];
            // update collision before loading

            // populate data (the children are already added on chunk construction)
            chunk.shape.Data = (Vector3[])chunk.mesh.Mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex];
        }

    }

    /// <summary>
    /// unload chunk collision, likely has to be deferred until chunk is stored to disk
    /// </summary>
    public static void unloadCollision(Int64 id, Dictionary<Int64, Chunk> idToChunk, HashSet<Int64> idToCol) {

        // if successfully found and removed, then reflect those changes, otherwise do nothing
        if (idToCol.Remove(id)) {
            // clear collision data
            Chunk chunk = idToChunk[id];

            chunk.shape.Data = [];
        }

        
    }


    /// <summary>
    /// load chunk from storage 
    /// returns 0 if chunk doesn't exist in filesystem
    /// returns 1 if chunk was succesfully loaded
    /// returns 2 if chunk was duplicated
    /// </summary>
    public static int loadChunk(Int64 id, Dictionary<Int64, Chunk> idToChunk) {
        if (idToChunk.ContainsKey(id)) return 2; // if already loaded don't load again

        // currently we don't have any way to check in file system
        return 0;
    }

    /// <summary>
    /// 
    /// </summary>
    public static void renderChunk(Int64 id, Dictionary<Int64, Chunk> idToChunk, HashSet<Int64> renderedChunks, Node3D root) {
        
        // doesn't run if add returns false (meaning duplicates exist)
        // this also means if rendered, don't render again
        if (renderedChunks.Add(id)) {
            Chunk chunk = idToChunk[id];
            root.AddChild(chunk);
            chunk.Position = chunk.chunkRes * (new Vector3(chunk.offsetX, chunk.offsetY, chunk.offsetZ));
        }

    }

    public static void unrenderChunk(Int64 id, Dictionary<Int64, Chunk> idToChunk, HashSet<Int64> renderedChunks, Node3D root) {

        // returns true if element is found and removed, otherwise returns false
        // so if no rendered chunk exists do nothing
        if (renderedChunks.Remove(id)) {
            // otherwise remove
            root.RemoveChild(idToChunk[id]);
        }
      
    }

    public static void unloadChunk(Int64 id, Dictionary<Int64, Chunk> chunkToId) {
        Chunk c1 = chunkToId[id];
        chunkToId.Remove(id);
        c1.QueueFree();
    }
    
}
