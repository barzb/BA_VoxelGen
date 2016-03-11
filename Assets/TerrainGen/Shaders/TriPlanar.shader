Shader "Custom/TriPlanar" 
{
	Properties
	{
		_Color("Main Color", Color) = (1,1,1,0.5)
		_SpecColor("Specular Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_Shininess("Shininess", Range(0.01, 2.0)) = 1.0
		_Bumpiness("Bumpiness", Range(0.0, 10.0)) = 1.0
		
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
		Tags{ "RenderType" = "Opaque" }
		LOD 400

		CGPROGRAM

		// Params: surfaceFunction lightModel [optionalparams]
		// add a vertex shader "vert" before the surface shader 
		#pragma surface surf BlinnPhong vertex:vert

		// ATTRIBUTES
		sampler2D _MainTexSIDE;
		sampler2D _MainTexTOP;
		sampler2D _MainTexDOWN;

		sampler2D _NormalMapSIDE;
		sampler2D _NormalMapTOP;
		sampler2D _NormalMapDOWN;

		half	  _Bumpiness;
		fixed4	  _Color;
		half	  _Shininess;
		half	  _TexScale;

		// INPUT STRUCTURES
		struct Input
		{
			float3 pos;
			float3 normal;
		};

		// VERTEX SHADER
		// appdate_full is a unity build-in structure with
		// position, tangent, normal, four texture coordinates and color.
		void vert(inout appdata_full v, out Input o)
		{
			o.pos = v.vertex;
			o.normal = v.normal;
		}

		// METHODS
		// calculate color of a texel with triplanar shading
		/// Note: This is the same code as the TriPlanar function in the 
		/// TriPlanarWithGrass Shader but without the LOD stuff
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

			// compute the UV coords for each of the 3 planar projections
			// _TexScale determines how big the textures appear
			half2 cX = pos.yz * _TexScale;
			half2 cY = pos.zx * _TexScale;
			half2 cZ = pos.xy * _TexScale;

			// Sample texture maps for each projection at UV coords with calculated LOD
			// left + right facing points get texSide
			half4 t_LR = tex2D(texSide, cX);
			half4 t_UD;
			// down facing points get texDown
			if (normal.y < 0) {
				t_UD = tex2D(texDown, cY);
			}
			// up facing points get texTop
			else {
				t_UD = tex2D(texTop, cY);
			}
			// front + back facing points get texSide, too
			half4 t_FB = tex2D(texSide, cZ);


			// use absolute value of normal as texture weights
			half3 blendWeights = abs(normal.xyz);
			// make sure the weights sum up to 1 (divide by sum of x+y+z)
			blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z).xxx;

			// ++ normal map mode ++
			if (normalMapping)
			{
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
				return fixed4(normalize(half3(0, 0, 1) + result.xyz * _Bumpiness), 0);
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

		// SURFACE SHADER
		void surf(Input IN, inout SurfaceOutput o)
		{
			// calculate rgb color of the texel
			fixed4 color = TriPlanar(IN.pos, IN.normal, false);
			// calculate bump vec of the texel
			fixed4 normalMap = TriPlanar(IN.pos, IN.normal, true);
			
			// set surface shader output
			o.Albedo	= color.rgb;
			o.Alpha		= color.a;
			o.Gloss		= color.a; // we don't use gloss maps so just use alpha
			o.Specular	= _Shininess;
			o.Normal	= normalMap;
		} 

		ENDCG
		
	} // SubShader
	FallBack "Diffuse"
} // Shader