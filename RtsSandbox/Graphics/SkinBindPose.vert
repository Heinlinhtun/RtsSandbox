#version 330 core
layout(location=0) in vec3 aPos;
layout(location=1) in vec4 aJoints;
layout(location=2) in vec4 aWeights;

uniform mat4 uVP;
uniform mat4 uModel;

void main()
{
    // bind pose (no animation yet)
    gl_Position = uVP * uModel * vec4(aPos, 1.0);
}
