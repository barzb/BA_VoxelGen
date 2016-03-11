Shader "Custom/TriplanarWithGrass"
{
	Properties
	{
		_LOD_Distance("LOD Distance", Range(0.1, 500.0)) = 100.0
		_Color("Main Color", Color) = (1,1,1,0.5)
		_SpecColor("Specular Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_Shininess("Shininess", Range(0.01, 2.0)) = 0.078125
		_Bumpiness("Bumpiness", Range(0.0, 10.0)) = 1.0
		_ShadowStrength("ShadowStrength", Range(0.0, 1.0)) = 0.7

		// GRASS PROPERTIES
		_GrassQuantity("GrassQuantity", Range(0.0, 100.0)) = 1.0
		_GrassSize("GrassSize", Range(0.01, 5.0)) = 0.1
		_GrassHealthyColor("GrassHealthyColor", Color) = (1.0, 1.0, 1.0, 1.0)
		_GrassDryColor("GrassDryColor", Color) = (1.0, 1.0, 1.0, 1.0)
		_GrassCutout("GrassCutout", Range(0.0, 1.0)) = 0.5
		[NoScaleOffset] _GrassTex("GrassTex", 2D) = "white" {}
		[NoScaleOffset] _GrassNoise("RGB_Noise", 2D) = "white" {}

		// TEXTURES FOR TRIPLANAR SHADING
		_TexScale("Tex Scale", Range(0.001, 10.0)) = 1.0

		[NoScaleOffset] _MainTexTOP("Texture Top", 2D) = "white" {}
		[NoScaleOffset] _NormalMapTOP("NormalMap Top ", 2D) = "bump" {}

		[NoScaleOffset] _MainTexSIDE("Texture Side", 2D) = "white" {}
		[NoScaleOffset] _NormalMapSIDE("NormalMap Side", 2D) = "bump" {}

		[NoScaleOffset] _MainTexDOWN("Texture Down", 2D) = "white" {}
		[NoScaleOffset] _NormalMapDOWN("NormalMap Down", 2D) = "bump" {}
	}
	SubShader
	{
		// #### NORMAL GEOMETRY ####
		Pass
		{
			// indicate that the pass is the "base" pass in forward
			// rendering pipeline. It gets ambient and main directional
			// light data set up; light direction in _WorldSpaceLightPos0
			// and color in _LightColor0
			Tags{ "RenderType" = "Opaque"  "LightMode" = "ForwardBase"}


			CGPROGRAM
			// complile these functions as vertex/fragment shader
			#pragma vertex	 vert
			#pragma fragment frag

			// compiles all variants needed by ForwardBase (forward rendering base) pass type. 
			// do not use anything other than the directional light for shadow mapping
			#pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight

			// adds option to the compiled OpenGL fragment program. 
			// The "fastest" option encourages the GL to minimize execution time, 
			// with possibly reduced precision.
			#pragma fragmentoption ARB_precision_hint_fastest
			
			// INCLUDES
			#include "UnityCG.cginc"	// for UnityObjectToWorldNormal
            #include "Lighting.cginc"	// for _LightColor0
            #include "AutoLight.cginc"	// for SHADOW_COORDS(#)

			// PASS ATTRIBUTES
			float	  _LOD_Distance;
			fixed4    _Color;
			float	  _Shininess;
			float	  _Bumpiness;
			float	  _ShadowStrength;
			half	  _TexScale;

			sampler2D _MainTexSIDE;
			sampler2D _MainTexTOP;
			sampler2D _MainTexDOWN;
			
			sampler2D _NormalMapSIDE;
			sampler2D _NormalMapTOP;
			sampler2D _NormalMapDOWN;

			// DATA STRUCTURES
			// vertex shader input
			struct appdata {
				float4 vertex	 : POSITION;
				float3 normal	 : NORMAL;
				float4 tangent	 : TANGENT;
			};
			// fragment shaderinput
			struct v2f {
				float4 pos			: SV_POSITION;
				SHADOW_COORDS(0)
				float3 WorldPos		: TEXCOORD1;
				float3 WorldNormal	: TEXCOORD2;
				float3 viewDir		: TEXCOORD3;
				float3 lightDir		: TEXCOORD4;
			};

			// METHODS
			// calculate color of a texel with triplanar shading
			fixed4 TriPlanar(float3 pos, float3 normal, bool normalMapping)
			{
				// get textures for rgb mapping
				sampler2D texTop  = _MainTexTOP;
				sampler2D texSide = _MainTexSIDE;
				sampler2D texDown = _MainTexDOWN;

				// get textures for normal mapping
				if (normalMapping)
				{
					texTop  = _NormalMapTOP;
					texSide = _NormalMapSIDE;
					texDown = _NormalMapDOWN;
				}	

				// far away texels (> maxDistance) will have damped details
				float maxDistance = _LOD_Distance;
				// calculate distance of texel to camera
				float distanceToPlayer = distance(mul(_World2Object, float4(_WorldSpaceCameraPos, 1)), pos);
				// calculate a LOD based on distance to camera
				float texLOD = max(0.0, (distanceToPlayer - maxDistance)) * 0.1;
				
				// compute the UV coords for each of the 3 planar projections
				// _TexScale determines how big the textures appear
				half2 cX = pos.yz * _TexScale;
				half2 cY = pos.zx * _TexScale;
				half2 cZ = pos.xy * _TexScale;

				// Sample texture maps for each projection at UV coords with calculated LOD
				// left + right facing points get texSide
				half4 t_LR = tex2Dlod(texSide, float4(cX, 0, texLOD));
				half4 t_UD;
				// down facing points get texDown
				if (normal.y < 0) {
					  t_UD = tex2Dlod(texDown, float4(cY, 0, texLOD));
				} 
				// up facing points get texTop
				else {
					  t_UD = tex2Dlod(texTop,  float4(cY, 0, texLOD));
				}
				// front + back facing points get texSide, too
				half4 t_FB = tex2Dlod(texSide, float4(cZ, 0, texLOD));


				// use absolute value of normal as texture weights
				half3 blendWeights = abs(normal.xyz);
				// make sure the weights sum up to 1 (divide by sum of x+y+z)
				blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z).xxx;

				// ++ normal map mode ++
				if (normalMapping)
				{
					// max distance to display normal mapping
					float bumpyMaxDistance = maxDistance / 5.0;
					// calculate a value between 1 (given bumpiness) and 0 (no bumpiness) based on distance to Player
					float bumpyDistance = (bumpyMaxDistance - min(bumpyMaxDistance, distanceToPlayer)) / bumpyMaxDistance;
					// damp the bumpiness over the calculated distance
					float bumpiness = max(0.0, _Bumpiness * bumpyDistance);
					
					// stretch the values from -1 to +1 instead of 0 to +1
					t_LR = t_LR * 2 - 1;
					t_UD = t_UD * 2 - 1;
					t_FB = t_FB * 2 - 1;

					// weight the colors from the 3 different textures
					half3 result = 
						t_LR.xyz * blendWeights.xxx +
						t_UD.xyz * blendWeights.yyy +
						t_FB.xyz * blendWeights.zzz;

					// calculate the bumpVec of the texel depending on bumpiness 
					return fixed4(normalize(half3(0, 0, 1) + result.xyz * bumpiness), 0);
				}

				// ++ rgb map mode ++
				// weight the colors from the 3 different textures
				half4 result =
					t_LR.xyzw * blendWeights.xxxx +
					t_UD.xyzw * blendWeights.yyyy +
					t_FB.xyzw * blendWeights.zzzz;

				// multiply with base color
				return result.rgba *_Color.rgba;
			}
			

			// VERTEX SHADER
			v2f vert(appdata v)
			{
				v2f o; // output
				// unity macro to initialize output data
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				// multiply position with modelViewProjection
				o.pos = mul(UNITY_MATRIX_MVP, v.vertex);

				// position and normal in world space is needed for triplanar shading in
				// the fragment shader
				o.WorldPos = v.vertex;
				o.WorldNormal = v.normal;
				

				// macro for unity to build the Object-Tangent rotation matrix "rotation"
				TANGENT_SPACE_ROTATION;
				// calculate view and light direction
				o.viewDir  = mul(rotation, ObjSpaceViewDir(v.vertex));
				o.lightDir = mul(rotation, ObjSpaceLightDir(v.vertex));
				// unity macro for shadow calculation
				TRANSFER_SHADOW(o);

				return o;
			}

			// FRAGMENT SHADER
			float4 frag(v2f input) : COLOR
			{
				// calculate rgb color of the texel
				fixed4 color = TriPlanar(input.WorldPos, input.WorldNormal, false);
				// calculate bump vec of the texel
				fixed4 normalMap = TriPlanar(input.WorldPos, input.WorldNormal, true);
				
				// normalize directions vectors
				input.viewDir = normalize(input.viewDir);
				input.lightDir = normalize(input.lightDir);

				// macro to get the combined shadow and attenuation value
				fixed atten = SHADOW_ATTENUATION(input);
				
				// calculate diffuse color from the normal map
				fixed diff = saturate(dot(normalMap, input.lightDir));

				// calculate specular color from the normal map
				half3 h = normalize(input.lightDir + input.viewDir);
				float nh = saturate(dot(normalMap, h));
				float spec = pow(nh, _Shininess * 128.0);

				// calculate diffuse color for the texel
				// multiply with light color and color from normal map
				fixed3 diffuse = color.rgb * _LightColor0.rgb * diff;

				fixed4 result;
				// now combine everything to get the RGB value
				// interpolate with shadow strength
				result.rgb = (diffuse + _SpecColor.rgb * spec) * (atten * 2) * _ShadowStrength;
				result.rgb += color.rgb * (1 - _ShadowStrength);
				// calculate alpha value
				result.a = color.a + _LightColor0.a * _SpecColor.a * spec * atten;
				return result;

			}
			ENDCG
		} // Pass
			
		/*--------------------------------------------------------------------------*/

		// #### GRASS ####
		Pass
		{ 
			// indicate that the pass is the "base" pass in forward
			// rendering pipeline. It gets ambient and main directional
			// light data set up; light direction in _WorldSpaceLightPos0
			// and color in _LightColor0
			Tags{ "LightMode" = "ForwardBase"  }

			CGPROGRAM

			// complile these functions as vertex/geometry/fragment shader
			#pragma vertex	 vert
			#pragma geometry geom
			#pragma fragment frag

			// compiles all variants needed by ForwardBase (forward rendering base) pass type. 
			// do not use anything other than the directional light for shadow mapping
			#pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight

			// adds option to the compiled OpenGL fragment program. 
			// The "fastest" option encourages the GL to minimize execution time, 
			// with possibly reduced precision.
			#pragma fragmentoption ARB_precision_hint_fastest

			// INCLUDES
			#include "UnityCG.cginc"	// for UnityObjectToWorldNormal
			#include "Lighting.cginc"	// for _LightColor0
			#include "AutoLight.cginc"	// for SHADOW_COORDS(#)

			// DATA STRUCTURES
			// vertex shader input
			struct appdata
			{
				float4 vertex : POSITION;
				float4 normal : NORMAL;
			};
			// geometry shader input
			struct v2g
			{
				float4 pos	  : SV_POSITION;
				float4 normal : TEXCOORD0;
			};
			// fragment shader input
			struct g2f
			{
				float4 pos	: SV_POSITION;
				float2 uv	: TEXCOORD0;
				SHADOW_COORDS(1)
				float4 tint	: COLOR0;
			};
			
			// PASS ATTRIBUTES
			float	  _LOD_Distance;
			sampler2D _GrassTex;
			sampler2D _GrassNoise;
			float	  _GrassQuantity;
			float	  _GrassSize;
			float4	  _GrassHealthyColor;
			float4	  _GrassDryColor;
			float	  _GrassCutout;
			float	  _ShadowStrength;
			
			// METHODS
			// returns a "random" positive value
			float rand(float2 offset, float max) {
				return abs(frac(sin(dot(offset.xy, float2(81.19462, 13.7189))) * 20621.6309)) % max;
			}

			// VERTEX SHADER
			v2g vert(appdata v)
			{
				// initialize output data
				v2g o;
				UNITY_INITIALIZE_OUTPUT(v2g, o);
				
				// set position and normal vec
				o.pos = mul(_Object2World, v.vertex);
				o.normal = v.normal;
				return o;
			}

			// GEOMETRY SHADER
			[maxvertexcount(72)]
			void geom(triangle v2g IN[3], inout TriangleStream<g2f> outStream)
			{
				// do not draw any grass if vertices are not facing up
				if (_GrassQuantity <= 0.0 || IN[0].normal.y < 0.3 
				  || IN[1].normal.y < 0.3 || IN[2].normal.y < 0.3) 
				{
					return;
				}

				// far away texels (> maxDistance) will have damped details
				float maxDistance = _LOD_Distance;
				// calculate distance of vertex to camera
				float distanceToPlayer = distance(float4(_WorldSpaceCameraPos, 1.0), IN[0].pos);
				// create less grass depending on distance to camera
				float numGrassByDistance = (maxDistance - min(maxDistance, max(0.0, distanceToPlayer - maxDistance/2.0))) / maxDistance;

				// calculate size and angle of the triangle
				float triSize = distance(IN[0].pos, IN[1].pos) * distance(IN[0].pos, IN[2].pos) / 2;
				float triAngle = max(0.0, IN[0].normal.y*2.0 - 0.9);
				
				// - make less grass on smaller triangles (triSize)
				// - make less grass on hillsides (triAngle)
				// - make less grass on distant triangles
				uint maxNumGrass = min(18, triSize * _GrassQuantity * triAngle * numGrassByDistance);

				// initialize output data
				g2f output;
				UNITY_INITIALIZE_OUTPUT(g2f, output);
				

				float2 noiseUV = float2(0,0);
				// create up to 18 billboards (= 72 vertices)
				for (uint i = 0; i < maxNumGrass; i++)
				{
					// calculate "random" barycentric coordinates from noise texture
					float3 bary = tex2Dlod(_GrassNoise, float4(noiseUV, 0, 0));
					
					// make even more "random" or every triangle will have the same grass
					bary.x = rand(IN[0].normal.y*190.01234 + bary.x, 1.0);
					bary.y = rand(IN[0].normal.z*841.24891 + bary.y, 1.0);
					bary.z = rand(IN[0].normal.x*209.84021 + bary.z, 1.0);

					// spread grass more away from triangle center
					bary.x *= bary.x;
					bary.y *= bary.y;
					bary.z *= bary.z;

					// next billboard will use different noise texture coordinate
					noiseUV += bary.xy;

					// "1..xxx" is the same as "float3(1,1,1)"
					// barycentric coordinates should be between 0 and 1
					bary /= dot(bary, 1..xxx);

					// calculate bottom center pos of the billboard
					float3 centerPos =
						bary.x * IN[0].pos.xyz +
						bary.y * IN[1].pos.xyz +
						bary.z * IN[2].pos.xyz;

					// move down a little (looks better on hillsides)
					centerPos.y -= 0.02;

					// calculate normal of the point in the triangle
					float3 midNorm = normalize(
						bary.x * IN[0].normal +
						bary.y * IN[1].normal +
						bary.z * IN[2].normal);
								
					// up vector
					float3 up = float3(0, 1, 0);
					// looking direction
					float3 look = _WorldSpaceCameraPos - centerPos;
					// don't rotate the y axis of the billboard
					look.y = 0;
					look = normalize(look);
					// calculate right vector
					float3 right = cross(up, look);

					// calculate value between 0.8 and 1.2
					float grassSizeVariation = rand(bary.xy*bary.yz*103.0197, 0.4) + 0.8;
					// final grass size can be between 0.8*grassSize and 1.2*grassSize
					float grassSize = _GrassSize * grassSizeVariation;

					// calculate 4 edge vertices of the billboard
					float4 v[4];
					v[0] = float4(centerPos + (grassSize /2 * right), 1.0);
					v[1] = float4(centerPos + (grassSize /2 * right) + (grassSize * up), 1.0);
					v[2] = float4(centerPos - (grassSize /2 * right), 1.0);
					v[3] = float4(centerPos - (grassSize /2 * right) + (grassSize * up), 1.0);
					
					// uv coordinates of the vertices
					float2 uvs[4];
					uvs[0] = float2(0, 0);
					uvs[1] = float2(0, 1);
					uvs[2] = float2(1, 0);
					uvs[3] = float2(1, 1);

					// ViewProjection matrix
					float4x4 vp = mul(UNITY_MATRIX_MVP, _World2Object);
					// calculate a color tint for the grass between healthy and dry color
					fixed lerpVal = bary.x;
					float4 tint = lerp(_GrassHealthyColor, _GrassDryColor, lerpVal);

					// add the vertices to the stream
					for (int j = 0; j < 4; j++)
					{
						output.pos = mul(vp, v[j]);
						output.uv = uvs[j];
						output.tint = tint;
						TRANSFER_SHADOW(output);
						outStream.Append(output);
					}
					
					// more vertices will not be edge-connected with the last ones
					outStream.RestartStrip();
				}
			}
			
			// FRAGMENT SHADER
			fixed4 frag(g2f input) : COLOR
			{
				// get color from grass texture and tint it
				fixed4 color = tex2D(_GrassTex, input.uv) * input.tint;

				// cut out transparent parts of the grass
				if (color.a < _GrassCutout)
					discard;

				// it looks nicer if the shadow is a little stonger on the grass
				_ShadowStrength = min(1.0, _ShadowStrength*2.0);
				
				// get light direction
				float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
				// macro to get the combined shadow and attenuation value
				float  atten = SHADOW_ATTENUATION(input);
				
				// calculate lighting stuff
				float3 ambient  = UNITY_LIGHTMODEL_AMBIENT.xyz;
				float3 normal   = float3(0, 1, 0);
				float3 lambert  = float(max(0.0, dot(normal, lightDirection)));
				float3 lighting = (ambient + lambert * atten) *_LightColor0.rgb;
				
				// interpolate with shadow strength
				fixed4 result = fixed4(color.rgb * lighting, 1.0f) *_ShadowStrength;
				result += color*(1 - _ShadowStrength);
				// make specColor a little brighter
				result += _SpecColor*0.2;
				return result;
			}
			ENDCG

		} // Pass

	} // SubShader
	FallBack "Diffuse"

} // Shader
