using Godot;
using Godot.Collections;
using System;
using System.Collections;

public enum EDGETEST{
	UPPEREDGE,
	INTERIOR,
	LOWEREDGE
}


public static class MarchCube{
	

	public static int[] getTriangulation(int px, int py, int pz, VoxelGrid vg) {
		int index = 0b00000000;

		index += vg.isIncluded(px, py, pz);

		index += (vg.isIncluded(px, py, pz + 1)) << 1;

		index += (vg.isIncluded(px + 1, py, pz + 1)) << 2;

		index += (vg.isIncluded(px + 1, py, pz)) << 3;

		index += (vg.isIncluded(px, py + 1, pz)) << 4;

		index += (vg.isIncluded(px, py + 1, pz + 1)) << 5;

		index += (vg.isIncluded(px + 1, py + 1, pz + 1)) << 6;

		index += (vg.isIncluded(px + 1, py + 1, pz)) << 7;


		return CubeData.TRIANGULATIONS[index];
	}

    public static int getTriangulationCount(int px, int py, int pz, VoxelGrid vg) {
        int index = 0b00000000;

        index += vg.isIncluded(px, py, pz);

        index += (vg.isIncluded(px, py, pz + 1)) << 1;

        index += (vg.isIncluded(px + 1, py, pz + 1)) << 2;

        index += (vg.isIncluded(px + 1, py, pz)) << 3;

        index += (vg.isIncluded(px, py + 1, pz)) << 4;

        index += (vg.isIncluded(px, py + 1, pz + 1)) << 5;

        index += (vg.isIncluded(px + 1, py + 1, pz + 1)) << 6;

        index += (vg.isIncluded(px + 1, py + 1, pz)) << 7;


        return CubeData.TRIANGULATIONCOUNT[index];
    }

    public static int marchCube(int px, int py, int pz, VoxelGrid vg, Cell cell, System.Collections.Generic.Dictionary<Int64, Chunk> neighbors) {
		int[] allEdges = getTriangulation(px, py, pz, vg);
		int i = 0;

		foreach (int edgeIndex in allEdges){
			if (edgeIndex == -1) break;

			Vector2I edgePair = CubeData.EDGES[edgeIndex];

			Vector3I p1 = CubeData.CORNERS[edgePair[0]];
			Vector3I p2 = CubeData.CORNERS[edgePair[1]];

			p1.X += px;
			p1.Y += py;
			p1.Z += pz;

			p2.X += px;
			p2.Y += py;
			p2.Z += pz;

			float t = vg.interpolateBetween(p1, p2);

			Vector3 p1f = (Vector3)p1;
			Vector3 p2f = (Vector3)p2;

			cell.positions[i] = t*(p1f-p2f)+p2f;

			p1f = vg.scalarFieldNormal(p1, neighbors);
			p2f = vg.scalarFieldNormal(p2, neighbors);

			cell.normals[i] = (t * (p1f - p2f) + p2f).Normalized();

			

			i++;
		}

        cell.active = i;
		return i;
    }

	public static void cellAllocate(int x, int y, int z, VoxelGrid vg, Cell c1) {
		c1.allocateArrays(MarchCube.getTriangulationCount(x, y, z, vg));
	}

	public static int marchCubes(System.Collections.Generic.Dictionary<Int64, Chunk> neighbors, VoxelGrid vg, Cell[] cells) {
		int i = 0;
		int maxSize = 0;
		int minVal = vg.minVal;
		int maxVal = vg.maxVal;

		for (int x = minVal; x < maxVal-1; x++)
		{
			for (int y = minVal; y < maxVal-1; y++)
			{
				for (int z = minVal; z < maxVal-1; z++)
				{
					cellAllocate(x, y, z, vg, cells[i]);
					maxSize += marchCube(x, y, z, vg, cells[i], neighbors);
                    i++;
                }
			}
		}

		return maxSize;
	}

	public static void resizeArrays(ref Vector3[] posResult, ref Vector3[] nResult, int activePositions) {
		Vector3[] newPos = new Vector3[activePositions];
		Vector3[] newNorm = new Vector3[activePositions];

		System.Array.Copy(posResult, newPos, activePositions);
		System.Array.Copy(nResult, newNorm, activePositions);

		posResult = newPos;
		nResult = newNorm;
	}

    public static Int64 packingX(int num) {
        return ((Int64)num << 42) & 0x7FFFFFFFFFFFFFFF;
    }

    public static Int64 packingY(int num) {
        return ((Int64)num << 21) & 0x000003FFFFFFFFFF;
    }

    public static Int64 packingZ(int num) {
        return (Int64)num & 0x00000000001FFFFF;
    }

    public static int unpackingX(Int64 id) {
		id = id & 0x7FFFFC0000000000;
        // 63rd bit is the x sign bit
        Int64 signedBit = id & 0x4000000000000000;
        // extract the signed bit, this is either 0 or not

        // left shift the signed bit to topmost position (if it's 0 it does nothing)
        signedBit = signedBit << 1;

        id = signedBit | id; // or it so it retains what's in the topmost bit

        // right shift c# automatically detects whether or not it should be arithmetic or logical shift
        id = id >> 42; // now retain the x val

        // downcast to int32 discards high order bits (retains lower 32 bits)
        return (int)id;
    }

    public static int unpackingY(Int64 id) {
        // just do packing X after shifting 21 up and clearing lower bits
        return unpackingX((id & 0x000003FFFFE00000) << 21);
    }

    public static int unpackingZ(Int64 id) {
        // just do packing X after shifting 42 up and clearing everything else
        return unpackingX((id & 0x00000000001FFFFF) << 42);
    }

    /// <summary>
    /// IT IS IMPERATIVE THAT dx, dy, and dz are valid integers from -2^20 to 2^20 -1 
    /// </summary>
    /// <param name="dx">x offset</param>
    /// <param name="dy">y offset</param>
    /// <param name="dz">z offset</param>
    /// <returns></returns>
    public static Int64 encodeID(int dx, int dy, int dz) {
        return (packingX(dx) | packingY(dy) | packingZ(dz));
    }

	public static Vector3I decodeID(Int64 id) {
		return new Vector3I(unpackingX(id),unpackingY(id),unpackingZ(id));
	}

    /// <summary>
    /// returns which chunk the point is in on borders, it will return the greater chunk offset
    /// for example if x = res, then x/res = 1, it returns chunk with offset 1 for x and not
    /// </summary>
    public static Chunk toChunkLocal(System.Collections.Generic.Dictionary<Int64, Chunk> idToChunk, int gx, int gy, int gz, int resHalf, int resLog2) {
        return
            idToChunk[encodeID(calcOffset(gx, resLog2, resHalf), calcOffset(gy, resLog2, resHalf), calcOffset(gz, resLog2, resHalf))];
    }

	public static int calcOffset(int coord, int resLog2, int resHalf) {

        return (coord + resHalf) >> resLog2;
    }

	/// <summary>
	/// checks if a given global point is on the boundary
	/// distinction between upper and lower edge because 
	/// </summary>
	public static EDGETEST edgeTest(int offset, int coord, int resLog2, int resHalf) {

		// offset means chunk center
		// chunk center - resHalf gives lower boundary
		// chunk center + resHalf gives upper boundary
		// upper boundary of the chunk is what we want to test for
		// recall chunks go from [lower bound, upper bound) we must -1 to put it in the current chunk
		if ((offset << resLog2) - resHalf == coord) {
			return EDGETEST.LOWEREDGE;
        }
		else {
			return EDGETEST.INTERIOR;
		}
    }

}
