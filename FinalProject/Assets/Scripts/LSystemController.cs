using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine.Rendering;
using Unity.Mathematics;
using quiet;
using System.Linq;
using UnityEditor;

[CustomEditor(typeof(LSystemController))]
public class LSystemControllerEditor : Editor
{
    private void OnEnable()
    {
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if(GUILayout.Button("Clear"))
        {
            ((LSystemController)target).Clear();
        }

        if(GUILayout.Button("Regenerate"))
        {
            ((LSystemController)target).Clear();
            ((LSystemController)target).GenerateWalk();
        }
    }
}

[ExecuteAlways]
public class LSystemController : MonoBehaviour {

    
    // for defining the language and rules
    Hashtable ruleHash = new Hashtable(100);

	public float initial_length = 2;
	public float initial_radius = 1.0f;
    List<byte> start; 
    List<byte> lang;
	GameObject contents;
	float angleToUse = 45f;
	public int iterations = 4;

    public Grid grid;

    public GameObject[] prefabs;

    [System.Flags]
    enum Direction
    {
        None = 0,
        Up = 1,
        Right = 2,
        Down = 4,
        Left = 8,
    }

    struct DirectionVector
    {
        public static readonly int2 Up = new(0, 1);
        public static readonly int2 Right = new(1, 0);
        public static readonly int2 Down = new(0, -1);
        public static readonly int2 Left = new(-1, 0);

        public static Direction ToDirection(int2 direction)
        {
            if (direction.x == 0 && direction.y == 1)
            {
                return Direction.Up;
            } 
            else if (direction.x == 1 && direction.y == 0)
            {
                return Direction.Right;
            }
            else if (direction.x == 0 && direction.y == -1)
            {
                return Direction.Down;
            }
            else
            {
                return Direction.Left;
            }
        }

        public static Direction InverseDirectionOf(int2 direction)
        {
            if (direction.x == 0 && direction.y == 1)
            {
                return Direction.Down;
            }
            else if (direction.x == 1 && direction.y == 0)
            {
                return Direction.Left;
            }
            else if (direction.x == 0 && direction.y == -1)
            {
                return Direction.Up;
            }
            else
            {
                return Direction.Right;
            }
        }
    }

	// for drawing lines
	public float lineWidth = 1.0f;
    Mesh lineMesh;
    struct vertexInfo
    {
        public Vector3 pos;     
        public Color32 color;
        public vertexInfo(Vector3 p, Color32 c)
        {
            pos = p;
            color = c;
        }
    }
    List<vertexInfo> vertices;
    List<int> indices;
    public Material lineMaterial;
    MeshFilter filter;

    Dictionary<string, byte> lookup = new(17);
    Dictionary<string, byte> walkLookup = new(11);

    public void Clear()
    {
        UnityEngine.Debug.Log(grid.transform.childCount);
        Queue<Transform> children = new(grid.transform.childCount);
        for(int i = 0; i < grid.transform.childCount; i++)
        {
            children.Enqueue(grid.transform.GetChild(i));
        }
        while(children.Count > 0)
        {
            DestroyImmediate(children.Dequeue().gameObject);
        }
    }

    public void GenerateWalk()
    {
        UnityEngine.Debug.Log("Iterating");
        run(iterations);
        printLang();
        
        displayWalk();
    }

    void Start ()
    {

        // for timing start/finish of the rule generation and display
        // can be commented out
        Stopwatch watch = new Stopwatch();

        watch.Start();
        // we set the start with the expected max size of the language iteration
        start = new List<byte>(100);
        // seed
        start.Add(0);

        // Walking generator
        // this l-system encodes instructions for a walking "agent" that generates the dungeon
        // This agent walks between cells, generating connections with each movement

        // rules:
        // X = Do Nothing = 0
        // ^ = Forward = 1
        // v = Backward = 2
        // > = Turn Right and Move = 3
        // < = Turn Left = 4
        // [ = Push State = 5
        // ] = Pop State = 6
        // + = Turn Right = 7
        // - = Turn Left = 8

        // add our rules to a lookup
        PushStringToLookup(walkLookup, "X,^,v,>,<,[,],+,-");

        ruleHash.Add(walkLookup["X"], new byte[][]
        {
            ConvertToByteArray("^")
        });

        ruleHash.Add(walkLookup["^"], new byte[][]
        {
            ConvertToByteArray("^", 70),
            ConvertToByteArray("^^", 15),
            ConvertToByteArray("+^[>]", 15)
        });

        ruleHash.Add(walkLookup[">"], new byte[][]
        {
            ConvertToByteArray("<", 10),
            ConvertToByteArray("[>>]", 10),
            ConvertToByteArray(">", 80)
        });

        GenerateWalk();
    }

    void PushStringToLookup(Dictionary<string, byte> lookup, string entry)
    {
        string[] commaSeperated = entry.Split(',');
        for(byte i = 0; i < commaSeperated.Length; i++)
        {
            lookup.Add(commaSeperated[i], i);
        }
    }

    byte[] ConvertToByteArray(string i, byte chance = 100)
    {
        List<byte> result = new();
        
        result.Add(chance);

        foreach(char r in i)
        {
            result.Add(walkLookup[r.ToString()]);
        }

        return result.ToArray();
    }

    byte ConvertToByte(string i)
    {
        return lookup[i];
    }


    void printLang()
    {
        string byteString = "";
        string rep = "";
        foreach(byte b in lang)
        {
            byteString += $"{b},";
            rep += walkLookup.First(kv => kv.Value == b).Key;
        }
        UnityEngine.Debug.Log(byteString);
        UnityEngine.Debug.Log(rep);

    }

    // Get a rule from a given letter that's in our array
    byte[] getRule( byte[] input) {		
		if (ruleHash.ContainsKey(input[0]))
        {
            byte[][] rules = (byte[][])ruleHash[input[0]];
            List<byte[]> possible = new List<byte[]>((byte[][])ruleHash[input[0]]);
            byte[] selected;
            byte chance = 0;
            do
            {
                selected = rules.RandomElement();
                possible.Remove(selected);
                chance += selected[0];
            } while (chance < UnityEngine.Random.Range(0, 101) || selected.Length == 0);
            return selected[1..];

        }
		return input;
	}
	
	// Run the lsystem iterations number of times on the start axiom.
    // note that this is double buffering
	void run(int iterations) {
    	List<byte> buffer1 = start;
        List<byte> buffer2 = new List<byte>(100);
        List<byte> currentList = buffer1;
        List<byte> newList = buffer2;
        byte[] singleByte = new byte[] { 0 };
        int currentCount = 0;

        for (int i = 0; i < iterations; i++) {
            currentCount = currentList.Count;
        	for (int j = 0; j < currentCount; j++) {
                singleByte[0] = currentList[j];
                byte[] buff = getRule(singleByte );
                newList.AddRange(buff);
        	}
            List<byte> tmp = currentList;
            currentList = newList;
            tmp.Clear();
            newList = tmp;
    	}
        
        lang = currentList;
    }

    Vector2Int EnsureInBounds(Vector2Int v)
    {
        if (v.x < 0)
        {
            v.x = 0;
            UnityEngine.Debug.Log("Out of bounds!!");
        }
        if (v.y < 0)
        {
            v.y = 0;
            UnityEngine.Debug.Log("Out of bounds!!");
        }

        return v;
    }

    void displayWalk()
    {
        int[,] worldMap = new int[200,200];
        for(int i = 0; i < worldMap.GetLength(0); i++)
        {
            for(int j = 0; j < worldMap.GetLength(1); j++)
            {
                worldMap[i, j] = 0;
            }
        }

        Stack<int> positions = new Stack<int>();
        Stack<int> directions = new Stack<int>();

        Vector2Int position = new(100, 100);
        int2 direction = DirectionVector.Up;


        printLang();

        // rules:
        // X = Do Nothing = 0
        // ^ = Forward = 1
        // v = Backward = 2
        // > = Turn Right and Move = 3
        // < = Turn Left and Move = 4
        // [ = Push State = 5
        // ] = Pop State = 6
        // + = Turn right = 7
        // - = Turn left = 8
        for (int i = 0; i < lang.Count; i++)
        {
            byte buff = lang[i];
            switch (buff)
            {
                // ^ - Forward
                case 1:
                    worldMap[position.x, position.y] |= (int)DirectionVector.ToDirection(direction);
                    position = new Vector2Int(position.x + direction.x, position.y + direction.y);
                    position = EnsureInBounds(position);
                    worldMap[position.x, position.y] |= (int)DirectionVector.InverseDirectionOf(direction);
                    break;
                case 2:
                    worldMap[position.x, position.y] |= (int)DirectionVector.InverseDirectionOf(direction);
                    position = new Vector2Int(position.x - direction.x, position.y - direction.y);
                    position = EnsureInBounds(position);
                    worldMap[position.x, position.y] |= (int)DirectionVector.ToDirection(direction);
                    break;
                case 3:
                    direction =
                        direction.Equals(DirectionVector.Up) ? DirectionVector.Right :
                        direction.Equals(DirectionVector.Right) ? DirectionVector.Down :
                        direction.Equals(DirectionVector.Down) ? DirectionVector.Left :
                        DirectionVector.Up;
                    worldMap[position.x, position.y] |= (int)DirectionVector.ToDirection(direction);
                    position = new Vector2Int(position.x + direction.x, position.y + direction.y);
                    position = EnsureInBounds(position);
                    worldMap[position.x, position.y] |= (int)DirectionVector.InverseDirectionOf(direction);
                    break;
                case 4:
                    direction =
                        direction.Equals(DirectionVector.Up) ? DirectionVector.Left :
                        direction.Equals(DirectionVector.Left) ? DirectionVector.Down :
                        direction.Equals(DirectionVector.Down) ? DirectionVector.Right :
                        DirectionVector.Up;
                    worldMap[position.x, position.y] |= (int)DirectionVector.ToDirection(direction);
                    position = new Vector2Int(position.x + direction.x, position.y + direction.y);
                    position = EnsureInBounds(position);
                    worldMap[position.x, position.y] |= (int)DirectionVector.InverseDirectionOf(direction);
                    break;
                case 5:
                    positions.Push(position.y);
                    positions.Push(position.x);
                    directions.Push(direction.y);
                    directions.Push(direction.x);
                    break;
                case 6:
                    position = new Vector2Int(positions.Pop(), positions.Pop());
                    //direction = new int2(directions.Pop(), directions.Pop());
                    break;
                case 7:
                    direction =
                        direction.Equals(DirectionVector.Up) ? DirectionVector.Right :
                        direction.Equals(DirectionVector.Right) ? DirectionVector.Down :
                        direction.Equals(DirectionVector.Down) ? DirectionVector.Left :
                        DirectionVector.Up;
                    break;
                case 8:
                    direction =
                        direction.Equals(DirectionVector.Up) ? DirectionVector.Left :
                        direction.Equals(DirectionVector.Left) ? DirectionVector.Down :
                        direction.Equals(DirectionVector.Down) ? DirectionVector.Right :
                        DirectionVector.Up;
                    break;

            }
        }

        // Draw Worldmap
        const float X_CONVERSION = 5.0f;
        const float Y_CONVERSION = 7.0f;

        for(int i = 0; i < worldMap.GetLength(0); i++)
        {
            for(int j = 0; j < worldMap.GetLength(1); j++)
            {
                switch((Direction)worldMap[i, j])
                {
                    case Direction.Up:
                        Instantiate(prefabs[0], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        Instantiate(prefabs[1], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        break;
                    case Direction.Down:
                        Instantiate(prefabs[0], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        Instantiate(prefabs[2], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        break;
                    case Direction.Up | Direction.Down:
                        if(UnityEngine.Random.Range(0, 101) > 50)
                        {
                            Instantiate(prefabs[0], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                            Instantiate(prefabs[3], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        } else
                        {
                            Instantiate(prefabs[17], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        }
                        break;
                    case Direction.Right:
                        Instantiate(prefabs[0], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        Instantiate(prefabs[4], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        break;
                    case Direction.Left:
                        Instantiate(prefabs[0], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        Instantiate(prefabs[5], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        break;
                    case Direction.Right | Direction.Left:
                        if (UnityEngine.Random.Range(0, 101) > 50)
                        {
                            Instantiate(prefabs[0], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                            Instantiate(prefabs[6], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        } else
                        {
                            Instantiate(prefabs[16], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        }
                        break;
                    case Direction.Left | Direction.Right | Direction.Up:
                        Instantiate(prefabs[0], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        Instantiate(prefabs[7], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        break;
                    case Direction.Left | Direction.Right | Direction.Down:
                        Instantiate(prefabs[0], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        Instantiate(prefabs[8], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        break;
                    case Direction.Up | Direction.Right | Direction.Down:
                        Instantiate(prefabs[0], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        Instantiate(prefabs[9], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        break;
                    case Direction.Up | Direction.Left | Direction.Down:
                        Instantiate(prefabs[0], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        Instantiate(prefabs[10], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        break;
                    case Direction.Up | Direction.Down | Direction.Left | Direction.Right:
                        Instantiate(prefabs[0], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        Instantiate(prefabs[11], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        break;
                    case Direction.Up | Direction.Right:
                        Instantiate(prefabs[0], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        Instantiate(prefabs[12], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        break;
                    case Direction.Up | Direction.Left:
                        Instantiate(prefabs[0], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        Instantiate(prefabs[13], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        break;
                    case Direction.Down | Direction.Right:
                        Instantiate(prefabs[0], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        Instantiate(prefabs[14], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        break;
                    case Direction.Down | Direction.Left:
                        Instantiate(prefabs[0], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        Instantiate(prefabs[15], new Vector3(i * X_CONVERSION, j * Y_CONVERSION, 0), Quaternion.identity, grid.transform);
                        break;
                }
            }
        }
    }

    void display()
    {
        Stack<float> positions = new();
        Stack<int> directions = new();

        Vector3 position = new(0, 0, 0);
        int2 direction = DirectionVector.Up;

        

        // Simple rule extends the dungeon by one each iteration
        // rules: R = 0, RT = 1, RB = 2, RV = 3,
        //               RR = 4, RL = 5, RH = 6,
        //               RHT = 7, RHB = 8,
        //               RVR = 9, RVL = 10,
        //               RC = 11
        //        [ = 12, ] = 13,
        //        + = 14, - = 15,
        //        M = 16

        // start at 0,0,0
        // Apply all the drawing rules to the lsystem string
        for (int i = 0; i < lang.Count; i++)
        {
            byte buff = lang[i];
            UnityEngine.Debug.Log($"Drawing rule {buff}");
            // These are rooms, draw the base
            if(buff > 0 && buff < 12)
            {
                // draw the base
                Instantiate(prefabs[0], position, Quaternion.identity, grid.transform);
                Instantiate(prefabs[buff], position, Quaternion.identity, grid.transform);
            }
            switch(buff)
            {
                // Seed room, should never draw
                case 0:
                    break;
                case 12:
                    positions.Push(position.y);
                    positions.Push(position.x);
                    directions.Push(direction.y);
                    directions.Push(direction.x);
                    break;
                case 13:
                    position = new Vector3(positions.Pop(), positions.Pop());
                    direction = new int2(directions.Pop(), directions.Pop());
                    break;
                // Rotate clockwise
                case 14:
                    direction =
                        direction.Equals(DirectionVector.Up)    ? DirectionVector.Right :
                        direction.Equals(DirectionVector.Right) ? DirectionVector.Down  :
                        direction.Equals(DirectionVector.Down)  ? DirectionVector.Left  :
                        DirectionVector.Up;
                    UnityEngine.Debug.Log("Direction is now: " + direction);
                    break;
                // Rotate counter-clockwise
                case 15:
                    direction =
                        direction.Equals(DirectionVector.Up)   ? DirectionVector.Left  :
                        direction.Equals(DirectionVector.Left) ? DirectionVector.Down  :
                        direction.Equals(DirectionVector.Down) ? DirectionVector.Right :
                        DirectionVector.Up;
                    break;
                // M
                case 16:
                    UnityEngine.Debug.Log($"Moving Forward... from {position} to {position + new Vector3(5.0f * direction.x, 7.0f * direction.y)}");
                    position += new Vector3(5.0f * direction.x, 7.0f * direction.y);
                    break;
                case 17:
                    directions.Push(direction.y);
                    directions.Push(direction.x);
                    break;
                case 18:
                    direction = new int2(directions.Pop(), directions.Pop());
                    break;
            }
        }
    }


    // The display routine for the weed type plant above
    void display3() {

        // to push and pop location and angles
        Stack<float> positions = new Stack<float>(100);
        Stack<float> angles = new Stack<float>(100);

        // current location and angle
        float angle = 0f;
        Vector3 position = new Vector3(0, 0, 0);
        float posy = 0.0f;
        float posx = 0.0f;

        // location and rotation to draw towards
        Vector3 newPosition;
        Vector2 rotated;

        // start at 0,0,0
        // Apply all the drawing rules to the lsystem string
        for (int i = 0; i < lang.Count; i++)
        {
            byte buff = lang[i];
            switch (buff)
            {
                case 0:
                    break;
                case 1:
                    // draw a line 
                    posy += initial_length;
                    newPosition = new float3(position.x, posy, 0);
                    rotated = rotate(position, new float3(position.x, posy, 0), angle);
                    newPosition = new float3(rotated.x, rotated.y, 0);
                    addLineToMesh(lineMesh, position, newPosition, Color.green);
                    // set up for the next draw
                    position = newPosition;
                    posx = newPosition.x;
                    posy = newPosition.y;
                    break;
                case 2:
                    // Turn right 25
                    angle += angleToUse;
                    break;
                case 3:
                    // Turn left 25
                    angle -= angleToUse;
                    break;
                case 4:
                    //[: push position and angle
                    positions.Push(posy);
                    positions.Push(posx);
                    float currentAngle = angle;
                    angles.Push(currentAngle);
                    break;
                case 5:
                    //]: pop position and angle
                    posx = positions.Pop();
                    posy = positions.Pop();
                    position = new Vector3(posx, posy, 0);
                    angle = angles.Pop();
                    break;
                default: break;
            }


        }
        // after we recreate the mesh we need to assign it to the original object
        MeshUpdateFlags flags = MeshUpdateFlags.DontNotifyMeshUsers & MeshUpdateFlags.DontRecalculateBounds &
            MeshUpdateFlags.DontResetBoneBounds & MeshUpdateFlags.DontValidateIndices;

        // set vertices
        int totalCount = vertices.Count;
        var layout = new[]
        {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4)
            };
        lineMesh.SetVertexBufferParams(totalCount, layout);
        lineMesh.SetVertexBufferData(vertices, 0, 0, totalCount, 0, flags);

        // set indices
        UnityEngine.Rendering.IndexFormat format = IndexFormat.UInt32;
        lineMesh.SetIndexBufferParams(totalCount, format);
        lineMesh.SetIndexBufferData(indices, 0, 0, totalCount, flags);

        // set submesh
        SubMeshDescriptor desc = new SubMeshDescriptor(0, totalCount, MeshTopology.Lines);
        desc.bounds = new Bounds();
        desc.baseVertex = 0;
        desc.firstVertex = 0;
        desc.vertexCount = totalCount;
        lineMesh.SetSubMesh(0, desc, flags);
    }

    // Display routine for 2d examples in the main program
    void display2()
    {

        // to push and pop location and angle
        Stack<float> positions = new Stack<float>();
        Stack<float> angles = new Stack<float>();

        // current angle and position
        float angle = 0f;
        float3 position = new float3(0, 0, 0);
        float posy = 0.0f;
        float posx = 0.0f;

        // positions to draw towards
        float3 newPosition;
        float2 rotated;

        // start at 0,0,0        

        // Apply the drawing rules to the string given to us
        for (int i = 0; i < lang.Count; i++)
        {
            byte buff = lang[i];
            switch (buff)
            {
                case 0:
                    // draw a line ending in a leaf
                    posy += initial_length;
                    newPosition = new float3(position.x, posy, 0);
                    rotated = rotate(position, new float3(position.x, posy, 0), angle);
                    newPosition = new float3(rotated.x, rotated.y, 0);
                    addLineToMesh(lineMesh, position, new float3(rotated.x, rotated.y, 0), Color.green);
                    //drawLSystemLine(position, new Vector3(rotated.x, rotated.y, 0), line, Color.red);
                    // set up for the next draw
                    position = newPosition;
                    posx = newPosition.x;
                    posy = newPosition.y;
                    addCircleToMesh(lineMesh, 0.45f, 0.45f, position, Color.magenta);
                    break;
                case 1:
                    // draw a line 
                    posy += initial_length;
                    newPosition = new float3(position.x, posy, 0);
                    rotated = rotate(position, new float3(position.x, posy, 0), angle);
                    newPosition = new float3(rotated.x, rotated.y, 0);
                    //drawLSystemLine(position, newPosition, line, Color.green);
                    addLineToMesh(lineMesh, position, newPosition, Color.green);
                    // set up for the next draw
                    position = newPosition;
                    posx = newPosition.x;
                    posy = newPosition.y;
                    break;
                case 6:
                    //[: push position and angle, turn left 45 degrees
                    positions.Push(posy);
                    positions.Push(posx);
                    float currentAngle = angle;
                    angles.Push(currentAngle);
                    angle -= 45;
                    break;
                case 9:
                    //]: pop position and angle, turn right 45 degrees
                    posx = positions.Pop();
                    posy = positions.Pop();
                    position = new float3(posx, posy, 0);
                    angle = angles.Pop();
                    angle += 45;
                    break;
                default: break;
            }
            // after we recreate the mesh we need to assign it to the original object
            MeshUpdateFlags flags = MeshUpdateFlags.DontNotifyMeshUsers & MeshUpdateFlags.DontRecalculateBounds &
                MeshUpdateFlags.DontResetBoneBounds & MeshUpdateFlags.DontValidateIndices;

            // set vertices
            var layout = new[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4)
            };
            lineMesh.SetVertexBufferParams(vertices.Count, layout);
            lineMesh.SetVertexBufferData(vertices, 0, 0, vertices.Count, 0, flags);

            // set indices
            UnityEngine.Rendering.IndexFormat format = IndexFormat.UInt32;
            lineMesh.SetIndexBufferParams(indices.Count, format);
            lineMesh.SetIndexBufferData(indices, 0, 0, indices.Count, flags);

            // set submesh
            SubMeshDescriptor desc = new SubMeshDescriptor(0, indices.Count, MeshTopology.Lines);
            desc.bounds = new Bounds();
            desc.baseVertex = 0;
            desc.firstVertex = 0;
            desc.vertexCount = vertices.Count;
            lineMesh.SetSubMesh(0, desc, flags);
        }
    }


    void addLineToMesh(Mesh mesh, float3 from, float3 to, Color color)
    {
        vertexInfo[] lineVerts = new vertexInfo[] { new vertexInfo(from, color), new vertexInfo(to, color) };
        int numberOfPoints = vertices.Count;
        int[] indicesForLines = new int[]{0+numberOfPoints, 1+numberOfPoints
        };
        vertices.AddRange(lineVerts);
        indices.AddRange(indicesForLines);
    }

    // rotate a line and return the position after rotation
    // Assumes rotation around the Z axis
    float2 rotate(float3 pivotPoint, float3 pointToRotate, float angle) {
   		float2 result;
   		float Nx = (pointToRotate.x - pivotPoint.x);
   		float Ny = (pointToRotate.y - pivotPoint.y);
   		angle = -angle * Mathf.PI/180f;
   		result = new float2(Mathf.Cos(angle) * Nx - Mathf.Sin(angle) * Ny + pivotPoint.x, Mathf.Sin(angle) * Nx + Mathf.Cos(angle) * Ny + pivotPoint.y);
   		return result;
	}
   

	// Draw a circle with the given parameters
	// Should probably use different stuff than the default
    void addCircleToMesh(Mesh mesh, float radiusX, float radiusY, Vector3 center, Color color) {
        int numberOfPoints = vertices.Count;
        float x;
        float y;
        float z = 0f;
		int segments = 15;
        float angle = (360f / segments);

        for (int i = 0; i < (segments + 1); i++) {

            x = Mathf.Sin (Mathf.Deg2Rad * angle) * radiusX + center.x;
            y = Mathf.Cos (Mathf.Deg2Rad * angle) * radiusY + center.y;

            vertices.Add(new vertexInfo (new Vector3(x, y, 0), color ));
            indices.Add(numberOfPoints + i);
            angle += (360f / segments);

        }
        
    }

}
