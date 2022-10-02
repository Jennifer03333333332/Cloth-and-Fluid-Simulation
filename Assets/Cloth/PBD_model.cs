using UnityEngine;
using System.Collections;

public class PBD_model : MonoBehaviour
{

    float t = 0.0333f; //time step
    float damping = 0.99f;
    int[] E;//edge / vertex pair
    float[] L;//initial edge length
    Vector3[] V;//velocity of vertex
    int iterative_times = 40;//initial 32, if 64, more like cloth, more stiff, less elasticity
    float gravity = -9.8f;
    int cloth_sidelength = 21;
    float alpha = 0.2f;

    public GameObject c_sphere;
    float radius = 2.7f * 0.6f;
    // Use this for initialization
    void Start()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        
        //Resize the mesh.
        int n = cloth_sidelength;
        Vector3[] X = new Vector3[n * n];
        Vector2[] UV = new Vector2[n * n];
        int[] T = new int[(n - 1) * (n - 1) * 6];
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                X[j * n + i] = new Vector3(5 - 10.0f * i / (n - 1), 0, 5 - 10.0f * j / (n - 1));
                UV[j * n + i] = new Vector3(i / (n - 1.0f), j / (n - 1.0f));
            }
        int t = 0;
        for (int j = 0; j < n - 1; j++)
            for (int i = 0; i < n - 1; i++)
            {
                T[t * 6 + 0] = j * n + i;
                T[t * 6 + 1] = j * n + i + 1;
                T[t * 6 + 2] = (j + 1) * n + i + 1;
                T[t * 6 + 3] = j * n + i;
                T[t * 6 + 4] = (j + 1) * n + i + 1;
                T[t * 6 + 5] = (j + 1) * n + i;
                t++;
            }
        mesh.vertices = X;
        mesh.triangles = T;
        mesh.uv = UV;
        mesh.RecalculateNormals();

        //Construct the original edge list
        int[] _E = new int[T.Length * 2];
        for (int i = 0; i < T.Length; i += 3)
        {
            _E[i * 2 + 0] = T[i + 0];
            _E[i * 2 + 1] = T[i + 1];
            _E[i * 2 + 2] = T[i + 1];
            _E[i * 2 + 3] = T[i + 2];
            _E[i * 2 + 4] = T[i + 2];
            _E[i * 2 + 5] = T[i + 0];
        }
        //Reorder the original edge list
        for (int i = 0; i < _E.Length; i += 2)
            if (_E[i] > _E[i + 1])
                Swap(ref _E[i], ref _E[i + 1]);
        //Sort the original edge list using quicksort
        Quick_Sort(ref _E, 0, _E.Length / 2 - 1);

        int e_number = 0;
        for (int i = 0; i < _E.Length; i += 2)
            if (i == 0 || _E[i + 0] != _E[i - 2] || _E[i + 1] != _E[i - 1])
                e_number++;

        E = new int[e_number * 2];
        for (int i = 0, e = 0; i < _E.Length; i += 2)
            if (i == 0 || _E[i + 0] != _E[i - 2] || _E[i + 1] != _E[i - 1])
            {
                E[e * 2 + 0] = _E[i + 0];
                E[e * 2 + 1] = _E[i + 1];
                e++;
            }

        L = new float[E.Length / 2];
        for (int e = 0; e < E.Length / 2; e++)
        {
            int i = E[e * 2 + 0];
            int j = E[e * 2 + 1];
            L[e] = (X[i] - X[j]).magnitude;
        }

        V = new Vector3[X.Length];
        for (int i = 0; i < X.Length; i++)
            V[i] = new Vector3(0, 0, 0);
    }

    void Quick_Sort(ref int[] a, int l, int r)
    {
        int j;
        if (l < r)
        {
            j = Quick_Sort_Partition(ref a, l, r);
            Quick_Sort(ref a, l, j - 1);
            Quick_Sort(ref a, j + 1, r);
        }
    }

    int Quick_Sort_Partition(ref int[] a, int l, int r)
    {
        int pivot_0, pivot_1, i, j;
        pivot_0 = a[l * 2 + 0];
        pivot_1 = a[l * 2 + 1];
        i = l;
        j = r + 1;
        while (true)
        {
            do ++i; while (i <= r && (a[i * 2] < pivot_0 || a[i * 2] == pivot_0 && a[i * 2 + 1] <= pivot_1));
            do --j; while (a[j * 2] > pivot_0 || a[j * 2] == pivot_0 && a[j * 2 + 1] > pivot_1);
            if (i >= j) break;
            Swap(ref a[i * 2], ref a[j * 2]);
            Swap(ref a[i * 2 + 1], ref a[j * 2 + 1]);
        }
        Swap(ref a[l * 2 + 0], ref a[j * 2 + 0]);
        Swap(ref a[l * 2 + 1], ref a[j * 2 + 1]);
        return j;
    }

    void Swap(ref int a, ref int b)
    {
        int temp = a;
        a = b;
        b = temp;
    }

    //position-based dynamics in a Jacobi fashion.
    void Strain_Limiting()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = mesh.vertices;

        //Apply PBD here.
        //define two temporary arrays sum x[] and sum n[] to store the sums of vertex position updates and vertex count updates

        //unity initial vector3[] == 0
        Vector3[] sum_x = new Vector3[vertices.Length];//the sums of vertex position updates. sum_x[vertex_index] = pos changes in total 
        int[] sum_n = new int[vertices.Length];//vertex count updates. sum_n[vertex_index] = counts
        //For each spring calculate the force, or for every edge e connecting i and j
        for (int e = 0; e < E.Length / 2.0f; e++)
        {
            //E[0] E[1] is the first edge, E[2] E[3] is the second edge,....
            int vi = E[e * 2];
            int vj = E[e * 2 + 1];
            Vector3 Lji = vertices[vi] - vertices[vj];//from i to j
            float Lji_length = Lji.magnitude;
            //L[e] * Lij / Lij_length : initial edge length * unit direction
            sum_x[vi] += 0.5f * (vertices[vi] + vertices[vj] + L[e] * Lji / Lji_length);
            
            sum_x[vj] += 0.5f * (vertices[vi] + vertices[vj] - L[e] * Lji / Lji_length);
            
            sum_n[vi] += 1;
            sum_n[vj] += 1;

        }

        for(int i = 0; i< vertices.Length; i++)
        {
            //?2
            if (i == 0 || i == cloth_sidelength -1) continue;
            V[i] += 1 / t * ((alpha * vertices[i] + sum_x[i]) / (alpha + sum_n[i]) - vertices[i]);
            vertices[i] = (alpha * vertices[i] + sum_x[i]) / (alpha + sum_n[i]);
        }

        mesh.vertices = vertices;
    }
    //impulse-based method
    void Collision_Handling()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        Vector3[] X = mesh.vertices;
        Vector3 sphere_center = c_sphere.transform.position;

        //For every vertex, detect collision and apply impulse if needed.

        //1 find colliding vertex
        for(int i = 0; i < X.Length; i++)
        {
            if((X[i] - sphere_center).sqrMagnitude < radius*radius)
            {

                V[i] += (sphere_center + radius*(X[i] - sphere_center).normalized - X[i]) / t;//  (target pos - now pos)/ t
                X[i] = sphere_center + radius * (X[i] - sphere_center).normalized;//xi = target position
            }
        }


        mesh.vertices = X;
    }

    // Update is called once per frame
    void Update()
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        Vector3[] X = mesh.vertices;

        for (int i = 0; i < X.Length; i++)
        {
            if (i == 0 || i == cloth_sidelength - 1) continue;
            //Initial Setup
            //1 Damp the velocity
            V[i] *= damping;
            //2 update the velocity by gravity. only affects y(height direction).  v = v0 + at
            V[i].y += gravity*t;
            //3 update x by v. x = x0 + vt
            X[i] += t * V[i];
        }
        mesh.vertices = X;

        for (int l = 0; l < iterative_times; l++)
            Strain_Limiting();

        Collision_Handling();

        mesh.RecalculateNormals();

    }


}

