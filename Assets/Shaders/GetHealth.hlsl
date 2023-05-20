#ifndef MYHLSLINCLUDE_INCLUDED
#define MYHLSLINCLUDE_INCLUDED

struct Boid2 {
    float3 position;
    float3 direction;
    float health;
};
StructuredBuffer<Boid2> boidsBuffer2;

void GetHealth_half(in float instanceID, out float Out)
{
    Out = boidsBuffer2[instanceID].health;
}

void GetHealth_float(in float instanceID, out float Out)
{
    Out = boidsBuffer2[instanceID].health;
}

#endif