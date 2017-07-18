// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using SysDiag = System.Diagnostics;
using System.IO;
using UnityEngine;

namespace HoloToolkit.Unity.SpatialMapping
{
    /// <summary>
    /// SimpleMeshSerializer converts a UnityEngine.Mesh object to and from an array of bytes.
    /// This class saves minimal mesh data (vertices and triangle indices) in the following format:
    ///    File header: vertex count (32 bit integer), triangle count (32 bit integer)
    ///    Vertex list: vertex.x, vertex.y, vertex.z (all 32 bit float)
    ///    Triangle index list: 32 bit integers
    /// </summary>
    public static class SimpleMeshSerializer
    {
        /// <summary>
        /// The mesh header consists of two 32 bit integers.
        /// </summary>
        private static int HeaderSize = sizeof(int) * 2;

        /// <summary>
        /// Serializes a list of Mesh objects into a byte array.
        /// </summary>
        /// <param name="meshes">List of Mesh objects to be serialized.</param>
        /// <returns>Binary representation of the Mesh objects.</returns>
        public static byte[] Serialize(IEnumerable<Mesh> meshes)
        {
            byte[] data;

            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    foreach (Mesh mesh in meshes)
                    {
                        WriteMesh(writer, mesh);
                    }

                    stream.Position = 0;
                    data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                }
            }

            return data;
        }

        /// <summary>
        /// Serializes a list of MeshFilter objects into a byte array.
        /// Transforms vertices into world space before writing to the file.
        /// </summary>
        /// <param name="meshes">List of MeshFilter objects to be serialized.</param>
        /// <returns>Binary representation of the Mesh objects.</returns>
        public static byte[] Serialize(IEnumerable<MeshFilter> meshes)
        {
            byte[] data = null;

            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    foreach (MeshFilter meshFilter in meshes)
                    {
                        WriteMesh(writer, meshFilter.sharedMesh, meshFilter.transform);
                    }

                    stream.Position = 0;
                    data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                }
            }

            return data;
        }

        /// <summary>
        /// Deserializes a list of Mesh objects from the provided byte array.
        /// </summary>
        /// <param name="data">Binary data to be deserialized into a list of Mesh objects.</param>
        /// <returns>List of Mesh objects.</returns>
        public static IEnumerable<Mesh> Deserialize(byte[] data)
        {
            List<Mesh> meshes = new List<Mesh>();

            using (MemoryStream stream = new MemoryStream(data))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    while (reader.BaseStream.Length - reader.BaseStream.Position >= HeaderSize)
                    {
                        meshes.Add(ReadMesh(reader));
                    }
                }
            }

            return meshes;
        }

        /// <summary>
        /// Writes a Mesh object to the data stream.
        /// </summary>
        /// <param name="writer">BinaryWriter representing the data stream.</param>
        /// <param name="mesh">The Mesh object to be written.</param>
        /// <param name="transform">If provided, will transform all vertices into world space before writing.</param>
        private static void WriteMesh(BinaryWriter writer, Mesh mesh, Transform transform = null)
        {
            SysDiag.Debug.Assert(writer != null);
            mesh.RecalculateNormals();
            // Write the mesh data.
            //WriteMeshHeader(writer, mesh.vertexCount, mesh.triangles.Length, mesh.normals.Length);
            WriteMeshHeader(writer, mesh.vertexCount, mesh.triangles.Length);
            WriteVertices(writer, mesh.vertices, transform);
            //WriteVertices(writer, mesh.normals, transform);
            WriteTriangleIndicies(writer, mesh.triangles);
        }

        /// <summary>
        /// Reads a single Mesh object from the data stream.
        /// </summary>
        /// <param name="reader">BinaryReader representing the data stream.</param>
        /// <returns>Mesh object read from the stream.</returns>
        private static Mesh ReadMesh(BinaryReader reader)
        {
            SysDiag.Debug.Assert(reader != null);

            int vertexCount = 0;
            int triangleIndexCount = 0;

            // Read the mesh data.
            ReadMeshHeader(reader, out vertexCount, out triangleIndexCount);
            Vector3[] vertices = ReadVertices(reader, vertexCount);
            int[] triangleIndices = ReadTriangleIndicies(reader, triangleIndexCount);

            // Create the mesh.
            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangleIndices;
            // Reconstruct the normals from the vertices and triangles.
            mesh.RecalculateNormals();

            return mesh;
        }

        /// <summary>
        /// Writes a mesh header to the data stream.
        /// </summary>
        /// <param name="writer">BinaryWriter representing the data stream.</param>
        /// <param name="vertexCount">Count of vertices in the mesh.</param>
        /// <param name="triangleIndexCount">Count of triangle indices in the mesh.</param>
        private static void WriteMeshHeader(BinaryWriter writer, int vertexCount, int triangleIndexCount)
        {
            SysDiag.Debug.Assert(writer != null);

            writer.Write(vertexCount);
            writer.Write(triangleIndexCount);

        }

        private static void WriteMeshHeader(BinaryWriter writer, int vertexCount, int triangleIndexCount, int normalCount)
        {
            SysDiag.Debug.Assert(writer != null);

            writer.Write(vertexCount);
            writer.Write(normalCount);

            writer.Write(triangleIndexCount);
            Debug.Log(System.String.Format("vertexCount: {0}\nnormal: {1}\nfaces: {2}", vertexCount, normalCount, triangleIndexCount));

        }

        /// <summary>
        /// Reads a mesh header from the data stream.
        /// </summary>
        /// <param name="reader">BinaryReader representing the data stream.</param>
        /// <param name="vertexCount">Count of vertices in the mesh.</param>
        /// <param name="triangleIndexCount">Count of triangle indices in the mesh.</param>
        private static void ReadMeshHeader(BinaryReader reader, out int vertexCount, out int triangleIndexCount)
        {
            SysDiag.Debug.Assert(reader != null);

            vertexCount = reader.ReadInt32();
            triangleIndexCount = reader.ReadInt32();
        }

        /// <summary>
        /// Writes a mesh's vertices to the data stream.
        /// </summary>
        /// <param name="reader">BinaryReader representing the data stream.</param>
        /// <param name="vertices">Array of Vector3 structures representing each vertex.</param>
        /// <param name="transform">If provided, will convert all vertices into world space before writing.</param>
        private static void WriteVertices(BinaryWriter writer, Vector3[] vertices, Transform transform = null)
        {
            SysDiag.Debug.Assert(writer != null);
            //Debug.Log(vertices);
            string debugLine = "";
            if (transform != null)
            {
                for (int v = 0, vLength = vertices.Length; v < vLength; ++v)
                {
                    Vector3 vertex = transform.TransformPoint(vertices[v]);
                    writer.Write(vertex.x);
                    writer.Write(vertex.y);
                    writer.Write(vertex.z);
                }
            }
            else
            {
                foreach (Vector3 vertex in vertices)
                {
                    writer.Write(vertex.x);
                    writer.Write(vertex.y);
                    writer.Write(vertex.z);
                    //Debug.Log(System.String.Format("{0}    {1}    {2}", vertex.x, vertex.y, vertex.z));
                    //debugLine += System.String.Format("{0}    {1}    {2}\n", vertex.x, vertex.y, vertex.z);
                }
            }
            //Debug.Log(debugLine);
        }

        private static void WriteNormals(BinaryWriter writer, Vector3[] vertices, Transform transform = null)
        {
            SysDiag.Debug.Assert(writer != null);
            Debug.Log(System.String.Format("Normals len("));
            string debugLine = "";
            if (transform != null)
            {
                for (int v = 0, vLength = vertices.Length; v < vLength; ++v)
                {
                    Vector3 vertex = transform.TransformPoint(vertices[v]);
                    writer.Write(vertex.x);
                    writer.Write(vertex.y);
                    writer.Write(vertex.z);
                }
            }
            else
            {
                foreach (Vector3 vertex in vertices)
                {
                    writer.Write(vertex.x);
                    writer.Write(vertex.y);
                    writer.Write(vertex.z);
                    //Debug.Log(System.String.Format("{0}    {1}    {2}", vertex.x, vertex.y, vertex.z));
                    //debugLine += System.String.Format("{0}    {1}    {2}\n", vertex.x, vertex.y, vertex.z);
                }
            }
            //Debug.Log(debugLine);
        }

        /// <summary>
        /// Reads a mesh's vertices from the data stream.
        /// </summary>
        /// <param name="reader">BinaryReader representing the data stream.</param>
        /// <param name="vertexCount">Count of vertices to read.</param>
        /// <returns>Array of Vector3 structures representing the mesh's vertices.</returns>
        private static Vector3[] ReadVertices(BinaryReader reader, int vertexCount)
        {
            SysDiag.Debug.Assert(reader != null);

            Vector3[] vertices = new Vector3[vertexCount];

            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = new Vector3(reader.ReadSingle(),
                                        reader.ReadSingle(),
                                        reader.ReadSingle());
            }

            return vertices;
        }

        /// <summary>
        /// Writes the vertex indices that represent a mesh's triangles to the data stream
        /// </summary>
        /// <param name="writer">BinaryWriter representing the data stream.</param>
        /// <param name="triangleIndices">Array of integers that describe how the vertex indices form triangles.</param>
        private static void WriteTriangleIndicies(BinaryWriter writer, int[] triangleIndices)
        {
            SysDiag.Debug.Assert(writer != null);
            //Debug.Log(triangleIndices);
            int i = 0;
            string debugLine = "";
            foreach (int index in triangleIndices)
            {
                writer.Write(index);
                if (i % 3 == 0) {
                    debugLine += "\n";
                }
                
                //debugLine += System.String.Format("{0} ", index);
                i += 1;
            }
            //Debug.Log(debugLine);
        }

        /// <summary>
        /// Reads the vertex indices that represent a mesh's triangles from the data stream
        /// </summary>
        /// <param name="reader">BinaryReader representing the data stream.</param>
        /// <param name="triangleIndexCount">Count of indices to read.</param>
        /// <returns>Array of integers that describe how the vertex indices form triangles.</returns>
        private static int[] ReadTriangleIndicies(BinaryReader reader, int triangleIndexCount)
        {
            SysDiag.Debug.Assert(reader != null);

            int[] triangleIndices = new int[triangleIndexCount];

            for (int i = 0; i < triangleIndices.Length; i++)
            {
                triangleIndices[i] = reader.ReadInt32();
            }

            return triangleIndices;
        }
    }
}