/*
//  Copyright (c) 2015 José Guerreiro. All rights reserved.
//
//  MIT license, see http://www.opensource.org/licenses/mit-license.php
//
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
//
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//  THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// ReSharper disable InconsistentNaming

namespace pings.outlines.fancy
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Camera))]
	public class FancyOutlineOnCamera : MonoBehaviour
	{
		// Settings in the inspector
		
		[Header("General settings")]
							public bool backCulling = true;
							public bool autoEnableOutlinesOnScriptEnabled;
		[Range(0, 1)]       public float alphaCutoff = 0.5f;
							public bool overdrawOutlines = true;

		private static int lineThickness => (int)(4 * PSettings.OutlineThicknessMultiplier);
		private static Shader outlineBufferShader => FancyOutlineBundle.BufferShader;
		private static ComputeShader computeShader => FancyOutlineBundle.OutlineShader;
		
		// Internal variables
		
		public static FancyOutlineOnCamera Instance { get; private set; }

		private static readonly int
			// Buffer
			B_MainTex_ID = Shader.PropertyToID("_MainTex"),
			B_AlphaCutoff_ID = Shader.PropertyToID("_OutlineAlphaCutoff"),
			B_Culling_ID = Shader.PropertyToID("_Culling"),
			B_Color_ID = Shader.PropertyToID("_Color"),
			// Textures
			Resolution_ID = Shader.PropertyToID("resolution"),
			MainTexture_ID = Shader.PropertyToID("source_tex"),
			OutlineTex_ID = Shader.PropertyToID("outline_tex"),
			Result_ID = Shader.PropertyToID("result_tex"),
			PassA_ID = Shader.PropertyToID("pass_a"),
			PassB_ID = Shader.PropertyToID("pass_b"),
			// Outlines
			LineSize_ID = Shader.PropertyToID("line_size"),
			LineSize2_ID = Shader.PropertyToID("line_size_2");

		private int 
			kernelInitID, 
			kernelLeftID, kernelRightID, kernelVerticalID, 
			kernelFinalizeID;
		
		private RenderTexture mainTex, outlineTex, resultTex, surfaceA, surfaceB;
		
		private readonly HashSet<FancyOutline> outlines = new HashSet<FancyOutline>();
							
		private float OutlineThickness => Mathf.Max(1, lineThickness 
		                                  * (Mathf.Min(sourceCamera.pixelWidth, sourceCamera.pixelHeight) / 1080f));
							


		// General
		private Camera sourceCamera, outlineCamera;
		private (int x,int y) screenSize;
		private (int x, int y) overdrawSize;
		private CommandBuffer commandBuffer;
		private Material outlineBufferMaterial;
		private readonly Dictionary<Outline, List<Material>> cachedMaterials = new Dictionary<Outline, List<Material>>();
		
		

		private void Awake()
		{
			if (Instance)
			{
				Destroy(this);
				throw new Exception("you can only have one outline camera in the scene");
			}

			Instance = this;
			sourceCamera = GetComponent<Camera>() ?? Camera.main ?? Camera.current;
		}

		private void Start()
		{
			CreateMaterials();

			LoadOutlineCamera();
			UpdateOutlineCamera();
			
			LoadKernels();
			LoadTextures();

			commandBuffer = new CommandBuffer();
			outlineCamera.AddCommandBuffer(CameraEvent.BeforeImageEffects, commandBuffer);
		}

		private void OnEnable()
		{
			var existingOutlines = FindObjectsOfType<FancyOutline>();
			
			if (autoEnableOutlinesOnScriptEnabled) foreach (var outline in existingOutlines)
			{
				outline.enabled = false;
				outline.enabled = true;
			}
			
			foreach (var outline in existingOutlines)
				outlines.Add(outline);
		}

		private void OnDisable()
		{
			DestroyTextures();
			DestroyMaterials();
		}

		private bool renderTheNextFrame;
		public void OnPreRender()
		{
			if (commandBuffer == null || outlines.Count == 0 && !renderTheNextFrame)
			    return;
			
			renderTheNextFrame = outlines.Count != 0;

			CreateMaterials();
			LoadTextures();
			UpdateProperties();
			UpdateOutlineCamera();
			
			commandBuffer.SetRenderTarget(outlineTex);
			commandBuffer.Clear();

			foreach (var outline in outlines)
			foreach (var (outlineRenderer, meshFilter, sharedMaterials) in outline.Data)
			{
				if (!outline || !outlineRenderer || ((1 << outline.gameObject.layer) & sourceCamera.cullingMask) == 0) continue; // skip outlines that are not in the camera's culling mask
				
				for (var submeshIndex = 0; submeshIndex < sharedMaterials.Length; submeshIndex++)
				{
					var material = GetMaterial(outline, sharedMaterials[submeshIndex]);
                    material.SetInt(B_Culling_ID, (int) (backCulling ? CullMode.Back : CullMode.Off));
                    material.SetColor(B_Color_ID, outline.outlineColor);
                    
					var sharedMesh = meshFilter ? meshFilter.sharedMesh : 
						outlineRenderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : null;
					
					if (sharedMesh && submeshIndex >= sharedMesh.subMeshCount)
						continue;
					
					commandBuffer.DrawRenderer(outlineRenderer, material, submeshIndex, 0);
				}
			}

			outlineCamera.Render();
			return;
			
			Material GetMaterial(FancyOutline outline, Material sharedMaterial)
			{
				if (!cachedMaterials.TryGetValue(outline, out var outlineMaterials))
				{
					outlineMaterials = new List<Material>();
					cachedMaterials[outline] = outlineMaterials;
				}

				Texture tex = sharedMaterial && sharedMaterial.HasProperty(B_MainTex_ID)
					? sharedMaterial.mainTexture : null;

				foreach (var cached in outlineMaterials)
					if (cached.mainTexture == tex)
						return cached;

				var mat = new Material(outlineBufferMaterial);
				mat.mainTexture = tex;
				mat.SetColor(B_Color_ID, outline.outlineColor);
				outlineMaterials.Add(mat);
				return mat;
			}
		}

		private void OnRenderImage(RenderTexture source, RenderTexture destination)
		{
			if (!computeShader || outlines.Count == 0)
			{
				Graphics.Blit(source, destination);
				return;
			}
			
			int overdrawGroupsX = Mathf.CeilToInt(overdrawSize.x / 8f),
				overdrawGroupsY = Mathf.CeilToInt(overdrawSize.y / 8f),
				baseGroupsX     = Mathf.CeilToInt(screenSize.x / 8f),
				baseGroupsY     = Mathf.CeilToInt(screenSize.y / 8f),
				halfGroupsX     = Mathf.CeilToInt((screenSize.x + overdrawSize.x) / 16f);
			
			Graphics.Blit(source, mainTex);
			computeShader.Dispatch(kernelInitID,     overdrawGroupsX, overdrawGroupsY, 1);
			
			computeShader.Dispatch(kernelLeftID,     halfGroupsX, overdrawGroupsY, 1);
			computeShader.Dispatch(kernelRightID,    halfGroupsX, overdrawGroupsY, 1);
			computeShader.Dispatch(kernelVerticalID, baseGroupsX, overdrawGroupsY, 1);
			
			computeShader.Dispatch(kernelFinalizeID, baseGroupsX, baseGroupsY, 1);
			
			Graphics.Blit(resultTex, destination);
		}
		
		

		private void LoadKernels()
		{
			kernelInitID     = computeShader.FindKernel("init");
			kernelLeftID     = computeShader.FindKernel("left");
			kernelRightID    = computeShader.FindKernel("right");
			kernelVerticalID = computeShader.FindKernel("vertical");
			kernelFinalizeID = computeShader.FindKernel("finalize");
		}
		
		private void UpdateProperties()
		{
			computeShader.SetInts(Resolution_ID, overdrawSize.x, overdrawSize.y);
			computeShader.SetInt(LineSize_ID, (int) Math.Floor(OutlineThickness));
			computeShader.SetInt(LineSize2_ID, (int) Math.Ceiling(OutlineThickness*OutlineThickness));
		}

		private void CreateMaterials() // if needed
		{
			if (!computeShader || !outlineBufferShader)
				throw new Exception("Compute Shader or Outline Buffer Shader is not assigned. Please assign them in the OutlineScriptOnCamera component.");
			
			if (!outlineBufferMaterial)
				outlineBufferMaterial = new Material(outlineBufferShader);
			
			Shader.SetGlobalFloat(B_AlphaCutoff_ID, alphaCutoff);
			
		}

		private void DestroyMaterials()
		{
			foreach (var material in cachedMaterials.Values.SelectMany(x=>x))
				Destroy(material);
			cachedMaterials.Clear();
			
			Destroy(outlineBufferMaterial);
			
			outlineBufferMaterial = null;
		}

		private void LoadOutlineCamera()
		{
			if (outlineCamera) return;

			outlineCamera = GetComponentsInChildren<Camera>().FirstOrDefault(c => c.name == "Outline Camera");
			if (outlineCamera) return;
			
			var cameraGameObject = new GameObject("Outline Camera");
			cameraGameObject.transform.parent = sourceCamera.transform;
			outlineCamera = cameraGameObject.AddComponent<Camera>();
		}

		private void UpdateOutlineCamera()
		{
		    outlineCamera.CopyFrom(sourceCamera);
		    
		    outlineCamera.targetTexture = outlineTex;
		    outlineCamera.renderingPath = RenderingPath.Forward;
		    outlineCamera.backgroundColor = Color.clear;
		    outlineCamera.clearFlags = CameraClearFlags.SolidColor;
		    outlineCamera.rect = new Rect(0,0,1,1);
		    outlineCamera.cullingMask = 0;
		    outlineCamera.enabled = false;
		    outlineCamera.allowHDR = false;
		    outlineCamera.depthTextureMode = DepthTextureMode.None;
		    outlineCamera.allowMSAA = false;
		    
		    
		    // float scaleX = (float) overdrawSize.x / screenSize.x;
		    float scaleY = (float) overdrawSize.y / screenSize.y;
		
		    float fovRad = outlineCamera.fieldOfView * Mathf.Deg2Rad;
		    float newFovRad = 2f * Mathf.Atan(Mathf.Tan(fovRad * 0.5f) * scaleY);
		    outlineCamera.fieldOfView = newFovRad * Mathf.Rad2Deg;
		    outlineCamera.aspect = (float) overdrawSize.x / overdrawSize.y;
		}

		private bool previousOverdrawState;
		private int previousLineThickness;
		private void LoadTextures()
		{
			if (mainTex && // Textures already exist
			    sourceCamera.pixelWidth == mainTex.width && sourceCamera.pixelHeight == mainTex.height && // Correct size
			    overdrawOutlines == previousOverdrawState && lineThickness == previousLineThickness) // Correct overdraw
				return;
			
			previousOverdrawState = overdrawOutlines;
			previousLineThickness = lineThickness;
			
			int lineSize = Mathf.CeilToInt(OutlineThickness);
			screenSize = (sourceCamera.pixelWidth, sourceCamera.pixelHeight);
			overdrawSize = overdrawOutlines ? (screenSize.x + 2*lineSize, screenSize.y + 2*lineSize) : screenSize;
			
			DestroyTextures();
			
			mainTex    = new RenderTexture(screenSize.x,   screenSize.y,   0, RenderTextureFormat.ARGBHalf);
			resultTex  = new RenderTexture(screenSize.x,   screenSize.y,   0, RenderTextureFormat.ARGBHalf) { enableRandomWrite = true };
			outlineTex = new RenderTexture(overdrawSize.x, overdrawSize.y, 0, RenderTextureFormat.ARGBHalf) { enableRandomWrite = true };
			surfaceA   = new RenderTexture(overdrawSize.x, overdrawSize.y, 0, RenderTextureFormat.RInt)     { enableRandomWrite = true };
			surfaceB   = new RenderTexture(overdrawSize.x, overdrawSize.y, 0, RenderTextureFormat.RInt)     { enableRandomWrite = true };
			
			mainTex.Create();
			resultTex.Create();
			outlineTex.Create();
			surfaceA.Create();
			surfaceB.Create();
			
			// Init
			computeShader.SetTexture(kernelInitID, OutlineTex_ID, outlineTex); // Input
			computeShader.SetTexture(kernelInitID, PassA_ID, surfaceA); // Output pass_a
			computeShader.SetTexture(kernelInitID, PassB_ID, surfaceB); // Output pass_b
			
			// Passes
			computeShader.SetTexture(kernelLeftID, PassA_ID, surfaceA); // Input pass_a
			computeShader.SetTexture(kernelLeftID, PassB_ID, surfaceB); // Output pass_b
			computeShader.SetTexture(kernelLeftID, OutlineTex_ID, outlineTex); // Expand outline color
			
			computeShader.SetTexture(kernelRightID, PassA_ID, surfaceA); // Input pass_a
			computeShader.SetTexture(kernelRightID, PassB_ID, surfaceB); // Input/Output pass_b
			computeShader.SetTexture(kernelRightID, OutlineTex_ID, outlineTex); // Expand outline color
			
			computeShader.SetTexture(kernelVerticalID, PassB_ID, surfaceB); // Input pass_b
			computeShader.SetTexture(kernelVerticalID, PassA_ID, surfaceA); // Output pass_a
			computeShader.SetTexture(kernelVerticalID, OutlineTex_ID, outlineTex); // Expand outline color
			
			// Finalize and outline
			computeShader.SetTexture(kernelFinalizeID, MainTexture_ID, mainTex); // Input main camera
			computeShader.SetTexture(kernelFinalizeID, PassA_ID, surfaceA); // Input pass_a
			computeShader.SetTexture(kernelFinalizeID, PassB_ID, surfaceB); // Input pass_a
			computeShader.SetTexture(kernelFinalizeID, OutlineTex_ID, outlineTex); // Input outline color
			computeShader.SetTexture(kernelFinalizeID, Result_ID, resultTex); // Output result
		}

		private void DestroyTextures()
		{
			RenderTexture[] textures = { mainTex, resultTex, outlineTex, surfaceA, surfaceB };
			foreach (var tex in textures)
			{
				tex?.Release();
				Destroy(tex);
			}
			mainTex = resultTex = outlineTex = surfaceA = surfaceB = null;
		}

		public void AddOutline(FancyOutline outline) => outlines.Add(outline);

		public void RemoveOutline(FancyOutline outline) => outlines.Remove(outline);
	}
}
