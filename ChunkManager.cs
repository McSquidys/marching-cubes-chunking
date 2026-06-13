using Godot;
using System;
using System.Collections.Generic;
using static Godot.HttpRequest;
/// <summary>
/// Controls all the chunk loading, editing, and unloading necessary, bulk of the logic is here
/// </summary>
public class ChunkManager {
	/// <summary>
	/// contains all loaded chunks
	/// </summary>
	public Dictionary<Int64, Chunk> idToChunk;
	public HashSet<Int64> renderedChunks; /// index into the active chunks array
	
	/// <summary>
	/// all currently loaded chunks with collision (renderedChunks dict) controls scene tree addition/removal
	/// </summary>
	public HashSet<Int64> colChunks;

	private HashSet<Int64> chunksToModify; // set containing chunks the rerun marching cubes algorithm on after deformation
	private Queue<Int64> chunkQueue; // queue to modify chunks on deformation


	public int colDist, loadDist, renderDistance, vRenderDistance, chunkRes, resHalf, resLog2;

	public ChunkManager() {
		chunkRes = 16; // always a power of 2
		resHalf = 8; // always a power of 2
		resLog2 = 4; // which bit is set (log_2(chunkRes))
		renderDistance = 4;
		loadDist = renderDistance + 1;
		colDist = 2;
		vRenderDistance = 1;
		idToChunk = new Dictionary<Int64, Chunk>();
		renderedChunks = new HashSet<Int64>();
		colChunks = new HashSet<Int64>();
	}

	public ChunkManager(int resLog2, int renderDistance) {
		this.resLog2 = resLog2;
		this.renderDistance = renderDistance;
		vRenderDistance = 1;
		loadDist = renderDistance + 1;
		colDist = 2;

		chunkRes = 1 << resLog2;

		resHalf = chunkRes >> 1;

		idToChunk = new Dictionary<Int64, Chunk>();
		renderedChunks = new HashSet<Int64>();
		colChunks = new HashSet<Int64>();
	}

	/// <summary>
	/// check if chunk needs to be generated or loaded from file system
	/// </summary>
	public bool chunkExists(Int64 id) {
		return idToChunk.ContainsKey(id);
		// we can scan saved chunks exactly once on world load and store it in a dictionary
	}
	
	/// <summary>
	/// create chunk and load it into the dictionary (acts as loaded into memory)
	/// </summary>
	public void createChunk(int dx, int dy, int dz) {
		Int64 id = MarchCube.encodeID(dx, dy, dz);
		if (chunkExists(id)) return;
		Chunk c1 = new Chunk(chunkRes, resLog2, dx, dy, dz);
		idToChunk[MarchCube.encodeID(dx, dy, dz)] = c1;
	}

	public void initializeWorld(Node3D root, Vector3I startPos) {
		updateWithCollisions(root, startPos.X, startPos.Y, startPos.Z);
	}

	/// <summary>
	/// load an area of renderDist + 1 around the player so for r = 4, visible area is 9x9, and loaded area is 11x11
	/// </summary>
	public void loadChunks(int playerX, int playerY, int playerZ) {
		int result = 0;
		Int64 id = 0;

		for (int x = -loadDist+playerX; x <= loadDist + playerX; x++) {
			for (int y = 0; y < 1; y++) {
				for (int z = -loadDist+playerZ; z <= loadDist + playerZ; z++) {
					result = ChunkLoader.loadChunk(MarchCube.encodeID(x, y, z), idToChunk);
					id = MarchCube.encodeID(x, y, z);

					// result = 0 means that chunk was not found in file system
					if (result == 0) {
						createChunk(x, y, z);
						idToChunk[id].marchZeCube(idToChunk);
						idToChunk[id].setMesh();
					}
				}
			}
		}
	}

	/// <summary>
	/// generated from the chunk mesh itself
	/// </summary>
	public void loadChunkCollision(int playerX, int playerY, int playerZ) {

		for (int x = -colDist + playerX; x <= colDist + playerX; x++) {
			for (int y = 0; y < 1; y++) {
				for (int z = -colDist + playerZ; z <= colDist + playerZ; z++) {
					ChunkLoader.loadCollision(MarchCube.encodeID(x, y, z), idToChunk, colChunks);
				 }
			}
		}
	}

	public void unloadChunkCollision(int playerX, int playerY, int playerZ) {
		List<Int64> chunksToUnload = new List<Int64>();

		foreach (Int64 id in idToChunk.Keys) {

			if (Math.Abs(MarchCube.unpackingX(id) - playerX) > colDist) {
				chunksToUnload.Add(id);
				continue;
			}

			if (Math.Abs(MarchCube.unpackingY(id) - playerY) + 1 > colDist) {
				chunksToUnload.Add(id);
				continue;
			}

			if (Math.Abs(MarchCube.unpackingZ(id) - playerZ) > colDist) {
				chunksToUnload.Add(id);
				continue;
			}
		}

		foreach (Int64 id in chunksToUnload) {
			ChunkLoader.unloadCollision(id, idToChunk, colChunks);
		}

	}

	/// <summary>
	/// renders (add to scene tree) chunks within render distance of player position
	/// everything should be loaded into idToChunks (as loading is called before)
	/// </summary>
	public void renderChunks(Node3D parent, int playerX, int playerY, int playerZ) {
		Int64 id = 0;
		for (int x = -renderDistance+playerX; x <= renderDistance+playerX; x++) {
			for (int y = 0; y < 1; y++) {
				for (int z = -renderDistance+playerZ; z <= renderDistance+playerZ; z++) {
					id = MarchCube.encodeID(x, y, z);
					// render in range chunks
					ChunkLoader.renderChunk(id, idToChunk, renderedChunks, parent);
				}
			}
		}
	}

	/// <summary>
	/// unrenders chunks that are further than visible render distance but doesn't unload them from memory
	/// </summary>
	public void unrenderChunks(int playerX, int playerY, int playerZ, Node3D parent) {
		List<Int64> chunksToUnrender = new List<Int64>();

		foreach (Int64 id in idToChunk.Keys) {
			if (Math.Abs(MarchCube.unpackingX(id) - playerX) > renderDistance) {
				chunksToUnrender.Add(id);
				continue;
			}

			if (Math.Abs(MarchCube.unpackingY(id) - playerY)+1 > vRenderDistance) {
				chunksToUnrender.Add(id);
				continue;
			}

			if (Math.Abs(MarchCube.unpackingZ(id) - playerZ) > renderDistance) {
				chunksToUnrender.Add(id);
				continue;
			}

		}

		foreach (Int64 id in chunksToUnrender) {
			ChunkLoader.unrenderChunk(id, idToChunk, renderedChunks, parent);
		}
	}

	/// <summary>
	/// unload chunks from memory when they get further than load distance
	/// </summary>
	public void unloadChunks(int playerX, int playerY, int playerZ) {
		List<Int64> chunksToUnload = new List<Int64>();

		foreach (Int64 id in idToChunk.Keys) {
			if (Math.Abs(MarchCube.unpackingX(id) - playerX) > loadDist) {
				chunksToUnload.Add(id);
				continue;
			}

			if (Math.Abs(MarchCube.unpackingY(id)-playerY)+1 > loadDist) {
				chunksToUnload.Add(id);
				continue;
			}

			if (Math.Abs(MarchCube.unpackingZ(id) - playerZ) > loadDist) {
				chunksToUnload.Add(id);
				continue;
			}
		}

		foreach (Int64 id in chunksToUnload) {
			ChunkLoader.unloadChunk(id, idToChunk);
		}
	}

	// chunk loader methods as well

	/// <summary>
	/// chunk system dictates that a collision rebuild also must happen
	/// player position update, single entry point for loading, rendering, unrendering, unloading
	/// </summary>
	public void updateWithCollisions(Node3D root, int playerX, int playerY, int playerZ) {
		// currently loadChunks takes the longest amount of time
		loadChunks(playerX, playerY, playerZ);
		renderChunks(root, playerX, playerY, playerZ);
		loadChunkCollision(playerX, playerY, playerZ);
		unrenderChunks(playerX, playerY, playerZ, root);
		unloadChunkCollision(playerX, playerY, playerZ);
		unloadChunks(playerX, playerY, playerZ);
	}

	/// <summary>
	/// collision does not need to be updated here as the chunkSystem dictates
	/// </summary>
	public void updateWithoutCollisions(Node3D root,int playerX, int playerY, int playerZ) {
		// currently loadChunks takes the longest amount of time
		loadChunks(playerX, playerY, playerZ);
		renderChunks(root, playerX, playerY, playerZ);
		unrenderChunks(playerX, playerY, playerZ, root);
		unloadChunks(playerX, playerY, playerZ);
	}

	

	

}
