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

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace pings.outlines.fancy
{
	public class FancyOutline : Outline
	{
		public List<(Renderer renderer, MeshFilter meshFilter, Material[] sharedMaterials)> Data { get; private set; }
		public Color outlineColor = Color.white;

		private void Awake()
		{
			Data = GetComponentsInChildren<Renderer>()
				.Select(r => (r, r.gameObject.GetComponent<MeshFilter>(), r.sharedMaterials))
				.ToList();
			
			outlineColor = Color.HSVToRGB(Random.value, 0.7f, 1);
		}
		
		public override void SetColor(Color color) => outlineColor = color;

		private void OnEnable() => FancyOutlineOnCamera.Instance?.AddOutline(this);

		private void OnDisable() => FancyOutlineOnCamera.Instance?.RemoveOutline(this);

	}
}
