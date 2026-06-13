using Godot;
using Godot.Collections;
using System;
using System.Collections;

public class Cell {
    /// <summary>
    /// 15 fixed size ppositions or 5 triangle configuartion 
    /// </summary>
    public Vector3[] positions;
    /// <summary>
    /// fixed size to 5 triangle configuration
    /// </summary>
    public Vector3[] normals;
    /// <summary>
    /// how many positions/normals are in use
    /// </summary>
    public int active = 0;

    public Cell() {
        positions = [];
        normals = [];
        active = 0;
    }

    public void allocateArrays(int size) {
        if (active == size) {
            return;   
        }
        positions = new Vector3[size];
        normals = new Vector3[size];
        active = size;
    }
}

[GlobalClass]
public partial class Chunk : StaticBody3D{


    /// <summary>
    /// there are exactly (res-1)^3 cells
    /// </summary>
    public Cell[] cells;

    /// <summary>
    /// Mesh of the object
    /// </summary>
    public MeshInstance3D mesh;

    /// <summary>
    /// collision shape
    /// </summary>
    public ConcavePolygonShape3D shape;

    /// <summary>
    /// parent of shape
    /// </summary>
    public CollisionShape3D colShape;

    public ArrayMesh aMesh;

    public StandardMaterial3D mat1;

    public Godot.Collections.Array attributes;

    /// <summary>
    /// resolution constnats
    /// </summary>
    public int chunkRes, sampleRes, resHalf, offsetX, offsetY, offsetZ, arrSize, resLog2;

    private int maxAlloc;

    public VoxelGrid vg;

    public float PlaneField(int _x, int y, int _z) {
        return (float)y;
    }

    public Chunk(int res, int resLog2, int dx, int dy, int dz){

        // set all values to default
        chunkRes = res; // stride is 16 normally for every chunk, also used for preallocation
        sampleRes = chunkRes + 1; // 17 for the underlying voxel grid for a range of [-8,8]
        resHalf = res / 2;
        this.resLog2 = resLog2;
        maxAlloc = 15 * chunkRes * chunkRes * chunkRes;
        offsetX = dx;
        offsetY = dy;
        offsetZ = dz;
        arrSize = 0;

        // default grid
        vg = new VoxelGrid(sampleRes, PlaneField);

        // allocate maximum number of spots
        cells = new Cell[chunkRes * chunkRes * chunkRes];

        // fill those with empty cells
        for (int i = 0; i < cells.Length; i++) {
            cells[i] = new Cell();
        }

        // now build the grid (this populates the data into the voxel grid)
        // but doesn't fill the cells
        vg.buildGrid();

        // set vg offsets too
        vg.setID(dx, dy, dz);

        // default mesh instance 
        mesh = new MeshInstance3D();
        // collision stuff
        shape = new ConcavePolygonShape3D();
        colShape = new CollisionShape3D();



        colShape.Shape = shape;

        this.AddChild(mesh);
        this.AddChild(colShape);


        // rendering stuff
        aMesh = new ArrayMesh();
        attributes = new Godot.Collections.Array();
        attributes.Resize((int)Mesh.ArrayType.Max);
        mat1 = new StandardMaterial3D();
        mat1.AlbedoTexture = GD.Load<Texture2D>("res://aod.jpg");
        mesh.Mesh = aMesh;
        mesh.MaterialOverride = mat1;

    }

    /// <summary>
    /// run the marching cubes algorithm
    /// </summary>
    public void marchZeCube(System.Collections.Generic.Dictionary<Int64, Chunk> neighbors) {
        arrSize = MarchCube.marchCubes(neighbors, vg, cells);
    }

    /// <summary>
    /// translate a global point into chunk coords and set its value value
    /// </summary>
    public void setGlobalPoint(int globalx, int globaly, int globalz, float val) {
        int lx = globalx -( offsetX << resLog2);
        int ly = globaly - (offsetY << resLog2);
        int lz = globalz - (offsetZ << resLog2);

        vg.setPoint(lx, ly, lz, val);
    }

    /// <summary>
    /// set position to the new negative value (used for digging)
    /// </summary>
    public void subSetGlobalPoint(int globalx, int globaly, int globalz, float val) {
        int lx = globalx - (offsetX << resLog2);
        int ly = globaly - (offsetY << resLog2);
        int lz = globalz - (offsetZ << resLog2);

        vg.setPoint(lx, ly, lz, - val);
    }

    /// <summary>
    /// based on current value take whatever is further from the surface (used for digging)
    /// <br></br>consider point with val [-1] and val [1] the surface is in between at 0
    /// <br></br>consider point with val [-1] and val [3] (if we edit the top point) now the surface is closer to val[-1]
    /// <br></br> so the maximum (further) distance is what we keep 
    /// </summary>
    public void subGlobalPoint(int globalx, int globaly, int globalz, float val) {
        int lx = globalx - (offsetX << resLog2);
        int ly = globaly - (offsetY << resLog2);
        int lz = globalz - (offsetZ << resLog2);

        float current = vg.readFromGrid(lx, ly, lz);

        current = Math.Max(current, - val);

        // if 

        vg.setPoint(lx, ly, lz, current);
    }


    /// <summary>
    /// set position to the new value (used for building)
    /// </summary>
    public void addSetGlobalPoint(int globalx, int globaly, int globalz, float val) {
        int lx = globalx - (offsetX << resLog2);
        int ly = globaly - (offsetY << resLog2);
        int lz = globalz - (offsetZ << resLog2);

        vg.setPoint(lx, ly, lz,  val);
    }

    /// <summary>
    /// based on current value take whatever is closer to the surface (used for building)
    /// <br></br>consider point with val [-1] and val [1] the surface is in between at 0
    /// <br></br>consider point with val [-1] and val [3] (if we edit the top point) now the surface is closer to val[-1]
    /// <br></br> so the maximum (closest) distance is what we keep [1] for building
    /// </summary>
    public void addGlobalPoint(int globalx, int globaly, int globalz, float val) {
        // translate to local chunk coords
        int lx = globalx - (offsetX << resLog2);
        int ly = globaly - (offsetY << resLog2);
        int lz = globalz - (offsetZ << resLog2);

        // find the current value
        float current = vg.readFromGrid(lx, ly, lz);

        //  check which is closer to the surface (current vs new)
        current = Math.Min(current, val);



        vg.setPoint(lx, ly, lz, current);
    }


    /// <summary>
    /// set the mesh based on the current cells
    /// </summary>
    public void setMesh() {
        int maxPosCount = arrSize;

       

        Vector3[] positions = new Vector3[maxPosCount];
        Vector3[] normals = new Vector3[maxPosCount];
        Vector2[] uvs = new Vector2[maxPosCount];
        Color[] cols = new Color[maxPosCount];

        int j = 0;

        foreach (Cell cell in cells) {
            for (int i = 0; i < cell.active; i += 3) {

              
                positions[i + j] = cell.positions[i];
                positions[i + j + 1] = cell.positions[i+1];
                positions[i + j + 2] = cell.positions[i+2];

                normals[i + j] = cell.normals[i];
                normals[i + j+1] = cell.normals[i+1];
                normals[i + j+2] = cell.normals[i+2];

                cols[i + j] = new Color(cell.normals[i].X, cell.normals[i].Y, cell.normals[i].Z);
                cols[i + j+1] = new Color(cell.normals[i+1].X, cell.normals[i+1].Y, cell.normals[i+1].Z);
                cols[i + j+2] = new Color(cell.normals[i+2].X, cell.normals[i+2].Y, cell.normals[i+2].Z);
                

                if (offsetX % 2 == 0 && offsetZ % 2 == 0) {
                    uvs[i + j] = new Vector2(389 / 2204.0f, 44 / 2150.0f);
                    uvs[i + j + 1] = new Vector2(37 / 2204.0f, 700 / 2150.0f);
                    uvs[i + j + 2] = new Vector2(743 / 2204.0f, 700 / 2150.0f);
                } else if (offsetX % 2 != 0 && offsetZ % 2 != 0) {
                    uvs[i + j] = new Vector2(1105 / 2204.0f, 24 / 2150.0f);
                    uvs[i + j + 1] = new Vector2(760 / 2204.0f, 700 / 2150.0f);
                    uvs[i + j + 2] = new Vector2(1452 / 2204.0f, 700 / 2150.0f);
                } else {
                    uvs[i + j] = new Vector2(1838 / 2204.0f, 22 / 2150.0f);
                    uvs[i + j + 1] = new Vector2(2151 / 2204.0f, 700 / 2150.0f);
                    uvs[i + j + 2] = new Vector2(1520 / 2204.0f, 700 / 2150.0f);
                }
                
            }
            j+= cell.active;
        }


        attributes[(int)Mesh.ArrayType.Vertex] = positions;
        attributes[(int)Mesh.ArrayType.Normal] = normals;
        attributes[(int)Mesh.ArrayType.TexUV] = uvs;
        attributes[(int)Mesh.ArrayType.Color] = cols;

        // clear previous data
        aMesh.ClearSurfaces();
        aMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, attributes);
       
    }

    public void toggleNormals() {
        mat1.VertexColorUseAsAlbedo = !mat1.VertexColorUseAsAlbedo;
        if (mat1.VertexColorUseAsAlbedo) mat1.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        else mat1.ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel;
    }

    public void setCollision() {
        shape.Data = (Vector3[])mesh.Mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex];
    }

}

