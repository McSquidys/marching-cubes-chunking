using System;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class ChunkSystem : Node3D{

	public CharacterBody3D player;

	public Vector3 playerPos;
	public Vector3I currentRenderPos;
	public Vector3I prevRenderPos;
	public Vector3I prevColPos;

	private ChunkManager manager;
	private int chunkDelta = 1;
	private int colDelta;

	private bool unrenderme = false;

	private bool rebuildMe = false;

	public int chunksRebuilt = 0;

	public override void _Ready() {
		manager = new ChunkManager(4, 4);
		colDelta = manager.colDist; // border is exactly colDist, so any interior delta would be >= this
		// just set it to 0,0,0 for now, player start pos later
		manager.initializeWorld(this, new Vector3I(0, 0, 0));
	}

	public void setPlayer(CharacterBody3D player) {
		this.player = player;
	}

	public int getResLog2() {
		return manager.resLog2;
	}

	public int getLoadedChunks() {
		return manager.idToChunk.Count;
	}

	public int getRenderedChunks() {
		return manager.renderedChunks.Count;
	}

	public int getColChunks() {
		return manager.colChunks.Count;
	}

	public override void _Process(double delta) {
		playerPos = player.GlobalPosition;
		currentRenderPos = calcClosestOffset(playerPos);
		//GD.Print(currentRenderPos, " ", prevRenderPos, " ", player.GlobalPosition);
		//GD.Print(manager.idToRenderedChunks.Count);
		
		if (unrenderme == true) {
			unrenderme = false;
			
		}



		if (rebuildMe || isRenderNewChunks(currentRenderPos, prevRenderPos, chunkDelta)) {
			prevRenderPos = currentRenderPos;
			// rendering is necessary, check if collision rebuild is necessary
			if (rebuildMe || isRenderNewChunks(currentRenderPos, prevColPos, colDelta)) {
				prevColPos = currentRenderPos;
				// if yes then update collisions
				manager.updateWithCollisions(this, currentRenderPos.X, currentRenderPos.Y, currentRenderPos.Z);
			}
			else {
				manager.updateWithoutCollisions(this, currentRenderPos.X, currentRenderPos.Y, currentRenderPos.Z);
			}

			if (rebuildMe) rebuildMe = false;
		}
	}

	public int calcOffset(int coord, int resLog2, int resHalf) {

		return (coord+resHalf) >> resLog2;
	}

	public Vector3I calcClosestOffset(Vector3 globalPos) {
		int x = (int)globalPos.X;
		int y = (int)globalPos.Y;
		int z = (int)globalPos.Z;


		return new Vector3I(calcOffset(x, manager.resLog2, manager.resHalf), calcOffset(y, manager.resLog2, manager.resHalf), calcOffset(z, manager.resLog2, manager.resHalf));
	}

	public static bool isRenderNewChunks(Vector3I currentRenderPos, Vector3I prevRenderPosition, int threshhold) {
		
		Vector3I delta = currentRenderPos - prevRenderPosition;

		if (Math.Abs(delta.X) >= threshhold) {
			return true;
		}
		else if (Math.Abs(delta.Y) >= threshhold) {
			return true;
		}
		else if (Math.Abs(delta.Z) >= threshhold) {
			return true;
		}
		else return false;
	}

	public void onTerrainHit(int globalx, int globaly, int globalz, bool isBuild){
		sphereHalfDeform(globalx, globaly, globalz, 5, isBuild);
	}

	public void sphereHalfDeform(int gx, int gy, int gz, int r, bool isBuild) {
		HashSet<Int64> marchThese = new HashSet<Int64>();

		//edgeTest(gx, gy, gz);

		int lx = 0;
		int ly = 0;
		int lz = 0;

		for (int x = -r+gx; x <= r+gx; x++) {
			for (int y = -r+gy; y <= gy; y++) {
				for (int z = -r + gz; z <= r + gz; z++) {
					lx = x - gx;
					ly = y - gy;
					lz = z - gz;
					marchThese.UnionWith(ChunkEditor.modifyGrid(manager.idToChunk, manager.resHalf, manager.resLog2, x, y, z, (float)Math.Sqrt((double)(lx*lx+ly*ly+lz*lz))-r, isBuild));
				}
			}
		}

		foreach (Int64 id in marchThese) {
			manager.idToChunk[id].marchZeCube(manager.idToChunk);
			manager.idToChunk[id].setMesh();
			manager.idToChunk[id].setCollision();
		}

		chunksRebuilt = marchThese.Count;

		rebuildMe = true;

	}

	public void edgeTest(int gx, int gy, int gz) {
		GD.Print(gx, " ", gy, " ", gz);
		int offX = MarchCube.calcOffset(gx, manager.resLog2, manager.resHalf);
		int offY = MarchCube.calcOffset(gy, manager.resLog2, manager.resHalf);
		int offZ = MarchCube.calcOffset(gz, manager.resLog2, manager.resHalf);
		GD.Print(offX, " ", offY, " ", offZ);

		GD.Print(manager.idToChunk[MarchCube.encodeID(0, 0, 0)].vg.readFromGrid(0, 0, 8));

		GD.Print(manager.idToChunk[MarchCube.encodeID(0, 0, 1)].vg.readFromGrid(0, 0, -8));
		return;

		GD.Print(MarchCube.edgeTest(offX, gx, manager.resLog2, manager.resHalf),
			MarchCube.edgeTest(offY, gy, manager.resLog2, manager.resHalf),
			MarchCube.edgeTest(offZ, gz, manager.resLog2, manager.resHalf)
			);

	}

	public void toggleNormal() {
		foreach (Int64 id in manager.idToChunk.Keys){
			manager.idToChunk[id].toggleNormals();
		}
	}

}
