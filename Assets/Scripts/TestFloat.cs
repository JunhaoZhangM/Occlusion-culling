using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class TestFloat : MonoBehaviour
{
    public float a= 0.923451112f;
    public float b= 0.923451113f;
    public uint c = ~0u;
    // Start is called before the first frame update
    void Start()
    {
        uint uinta = math.asuint(a);
        uint uintb = math.asuint(b);
        Debug.LogFormat("a:{0} b:{1} asint.a:{2} asint.b:{3} a{4}b", a, b, uinta, uintb, uinta < uintb ? "<" : ">=");
        Debug.Log(c >> 33);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
