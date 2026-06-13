using System;
using System.Collections.Generic;
using Godot;


/// <summary>
/// The actual voxel grid <br></br>
/// Contains a float array for points, a surface level for inclusion
/// and a max and a min val for marching bounds
/// </summary>
[GlobalClass]
public partial class VoxelGrid : Resource{ 
	/// <summary>
	/// resolution (how many sample points for the grid)
	/// </summary>
	private int sampleRes;
	private int chunkRes;
	/// <summary>
	/// res*res
	/// </summary>
	private int res2; 
	/// <summary>
	/// res*0.5
	/// </summary>
	private int resHalf; 
	   
	public int maxVal; // maximum x,y,z value
	public int minVal; // minimum x,y,z value

	private int offsetX, offsetY, offsetZ;

	public float surfaceLevel = 0.0f; // test for inclusion

	private float[] points; // the data 

	/// <summary>
	/// the actual scalar field (points getting mapped to floats) 
	/// </summary>
	private Func<int, int, int, float> scField; // scalar field 


	/// <summary>
	/// construct a grid given the resolution and scalar field function
	/// </summary>
	/// <param name="reso"> resolution </param>
	/// <param name="scfield"> scalar field function </param>
	public VoxelGrid(int reso, Func<int, int, int, float> scfield) {
		// on creation
		sampleRes = reso;
		chunkRes = reso - 1;
		res2 = reso * reso;
		resHalf = reso / 2;
		scField = scfield;
		minVal = -sampleRes / 2;
		maxVal = (sampleRes % 2 == 0) ? sampleRes / 2 : sampleRes / 2 + 1;
		points = new float[sampleRes*sampleRes*sampleRes];

	}

	/// <summary>
	/// read from the underlying grid
	/// </summary>
	/// <param name="px">x val</param>
	/// <param name="py">y val</param>
	/// <param name="pz">z val</param>
	/// <returns></returns>
	public float readFromGrid(int px, int py, int pz) {
		return points[(px + resHalf) * res2 + (py + resHalf) * sampleRes + (pz + resHalf)];
	}

	public void setPoint(int px, int py, int pz, float val) {
		points[(px + resHalf) * res2 + (py + resHalf) * sampleRes + (pz + resHalf)] = val;
	}

	private void addPoint(int px, int py, int pz) {
		setPoint(px, py, pz, scField(px,py,pz));
	}

	private float evaluateField(int px, int py, int pz) {
		return scField(px, py, pz);
	}

	private float centralDifference(Vector3I p, Vector3I forwardD, Vector3I backwardD) {
		return readFromGrid(p.X + forwardD.X, p.Y + forwardD.Y, p.Z + forwardD.Z) - readFromGrid(p.X + backwardD.X, p.Y + backwardD.Y, p.Z + backwardD.Z);
	}

	public void setID(int x, int y, int z) {
		offsetX = x;
		offsetY = y;
		offsetZ = z;
	}

	

	/// <summary>
	/// p is the point from our neighbor in it's local space
	/// if it exists replace b with p in f(b)-f(a)
	/// if it doesn't exist do normal f(b)-f(a)
	/// </summary>
	private float queryForward(Dictionary<Int64, Chunk> neighbors, Vector3I p, float a, float b, Int64 id) {

        Chunk neighbor;
		float avgDenom = 1.0f;

        if (neighbors.TryGetValue(id, out neighbor)) {
			// if it does exist, replace b with p
			b = neighbor.vg.readFromGrid(p.X, p.Y, p.Z);
			// and take a normal avg
			avgDenom = 0.5f;
			
        }

		return (b - a)*avgDenom;

    }

    /// <summary>
    /// p is the point from our neighbor in it's local space
    /// if it exists replace p with a in f(b)-f(a)
    /// if it doesn't exist do normal f(b)-f(a)
    /// </summary>
    private float queryBackward(Dictionary<Int64, Chunk> neighbors, Vector3I p, float a, float b, Int64 id) {

        Chunk neighbor;
        float avgDenom = 1.0f;

        if (neighbors.TryGetValue(id, out neighbor)) {
            // if it does exist, replace a with p
            a = neighbor.vg.readFromGrid(p.X, p.Y, p.Z);
            // and take a normal avg
            avgDenom = 0.5f;

        }

        return (b - a) * avgDenom;
    }

    public Vector3 scalarFieldNormal(Vector3I p, Dictionary<Int64, Chunk> neighbors) {
		float dfdx, dfdy, dfdz;

		// x coord
		if (p.X <= minVal) {
			// query neighbor chunk
			// ideally here we want -1 in the x axis

			// transform to its local, and then query the neighbor that holds that point

			dfdx = queryBackward(neighbors, new Vector3I(p.X+chunkRes-1, p.Y, p.Z),  readFromGrid(p.X, p.Y, p.Z) , readFromGrid(p.X + 1, p.Y, p.Z), MarchCube.encodeID(offsetX-1, offsetY, offsetZ));

		} else if (p.X >= maxVal - 1) {
			// query neighbor chunk

			// f(b) - f(a)
			// b is +1 on x, a is -1
			dfdx = queryForward(neighbors, new Vector3I(p.X - chunkRes+1, p.Y, p.Z), readFromGrid(p.X-1, p.Y, p.Z), readFromGrid(p.X, p.Y, p.Z), MarchCube.encodeID(offsetX + 1, offsetY, offsetZ));

        } else {
			dfdx = (readFromGrid(p.X + 1, p.Y, p.Z) - readFromGrid(p.X - 1, p.Y, p.Z)) / (2.0f);
		}

		if (p.Y <= minVal) {
			// query neighbor
			dfdy = queryBackward(neighbors, new Vector3I(p.X, p.Y + chunkRes-1, p.Z), readFromGrid(p.X, p.Y, p.Z), readFromGrid(p.X, p.Y+1, p.Z), MarchCube.encodeID(offsetX, offsetY-1, offsetZ));
        } else if (p.Y >= maxVal - 1) {
			// query neighbor
			dfdy = queryForward(neighbors, new Vector3I(p.X, p.Y-chunkRes+1, p.Z), readFromGrid(p.X, p.Y-1, p.Z), readFromGrid(p.X, p.Y, p.Z), MarchCube.encodeID(offsetX, offsetY+1, offsetZ));
        } else {
			dfdy = (readFromGrid(p.X, p.Y + 1, p.Z) - readFromGrid(p.X, p.Y - 1, p.Z)) / (2.0f);
		}

		if (p.Z <= minVal) {
			// query neighbor first
			dfdz = queryBackward(neighbors, new Vector3I(p.X, p.Y, p.Z + chunkRes-1), readFromGrid(p.X, p.Y, p.Z), readFromGrid(p.X, p.Y, p.Z + 1), MarchCube.encodeID(offsetX, offsetY, offsetZ - 1));
		} else if (p.Z >= maxVal - 1) {
			// query neighbor first
			dfdz = queryForward(neighbors, new Vector3I(p.X, p.Y, p.Z-chunkRes+1), readFromGrid(p.X, p.Y, p.Z-1), readFromGrid(p.X, p.Y, p.Z), MarchCube.encodeID(offsetX, offsetY, offsetZ+1));
		} else {
			dfdz = (readFromGrid(p.X, p.Y, p.Z + 1) - readFromGrid(p.X, p.Y, p.Z - 1)) / (2.0f);
		}


		return new Vector3(dfdx, dfdy, dfdz).Normalized();

	}

	public int isIncluded(int px, int py, int pz) {
		return (readFromGrid(px, py, pz) < surfaceLevel) ? 1 : 0;
	}

	public void buildGrid() { 
		for (int x = minVal; x < maxVal; x++) {
			for (int y = minVal; y < maxVal; y++) {
				for (int z = minVal; z < maxVal; z++) {
					addPoint(x,y,z);
				}
			}
		}
	}

	public float interpolateBetween(Vector3I p, Vector3I q) {
		float a = readFromGrid(p.X, p.Y, p.Z);
		float b = readFromGrid(q.X, q.Y, q.Z);
		if (Math.Abs(a - b) < 0.001f) {
			return 0.5f;
		}

		float t = Math.Abs(surfaceLevel - a) / Math.Abs(a - b);

		return 1.0f - t;

	}
}

