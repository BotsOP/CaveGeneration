#ifndef SHADER_GRAPH_SUPPORT_H
#define SHADER_GRAPH_SUPPORT_H

struct Boid {
    float3 position;
    float3 direction;
    float health;
};
StructuredBuffer<Boid> boidsBuffer;

float4x4 create_matrix(float3 pos, float3 dir, float3 up)
{
    float3 scale = float3(20, 20, 20);
    float3 zaxis = normalize(dir);
    float3 xaxis = normalize(cross(up, zaxis));
    float3 yaxis = cross(zaxis, xaxis);
    return float4x4(
        xaxis.x * scale.x, yaxis.x * scale.y, zaxis.x * scale.z, pos.x,
        xaxis.y * scale.x, yaxis.y * scale.y, zaxis.y * scale.z, pos.y,
        xaxis.z * scale.x, yaxis.z * scale.y, zaxis.z * scale.z, pos.z,
        0, 0, 0, 1
    );
}

inline void SetUnityMatrices(uint instanceID, inout float4x4 objectToWorld, inout float4x4 worldToObject)
{
#if UNITY_ANY_INSTANCING_ENABLED
    Boid boid = boidsBuffer[instanceID];
  
    float3 scale = float3(1, 1, 1);
    objectToWorld = mul(create_matrix(boid.position, boid.direction, float3(0, 1, 0)), objectToWorld);

    float3x3 w2oRotation;
    w2oRotation[0] = objectToWorld[1].yzx * objectToWorld[2].zxy - objectToWorld[1].zxy * objectToWorld[2].yzx;
    w2oRotation[1] = objectToWorld[0].zxy * objectToWorld[2].yzx - objectToWorld[0].yzx * objectToWorld[2].zxy;
    w2oRotation[2] = objectToWorld[0].yzx * objectToWorld[1].zxy - objectToWorld[0].zxy * objectToWorld[1].yzx;

    float det = dot(objectToWorld[0].xyz, w2oRotation[0]);
    w2oRotation = transpose(w2oRotation);
    w2oRotation *= rcp(det);
    float3 w2oPosition = mul(w2oRotation, -objectToWorld._14_24_34);

    worldToObject._11_21_31_41 = float4(w2oRotation._11_21_31, 0.0f);
    worldToObject._12_22_32_42 = float4(w2oRotation._12_22_32, 0.0f);
    worldToObject._13_23_33_43 = float4(w2oRotation._13_23_33, 0.0f);
    worldToObject._14_24_34_44 = float4(w2oPosition, 1.0f);
#endif
}

void passthroughVec3_float(in float3 In, out float3 Out)
{
    Out = In;
}

void GetHealth_half(in float instanceID, out float Out)
{
    Out = boidsBuffer[instanceID].health;
}

void GetHealth_float(in float instanceID, out float Out)
{
    Out = boidsBuffer[instanceID].health;
}

void setup()
{
#if UNITY_ANY_INSTANCING_ENABLED
    SetUnityMatrices(unity_InstanceID, unity_ObjectToWorld, unity_WorldToObject);
#endif
}

#endif