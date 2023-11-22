﻿using System.Collections.Generic;
using UnityEngine;

namespace FairyGUI
{
	/// <summary>
	/// 
	/// </summary>
	public class BitmapFont : BaseFont
	{
		/// <summary>
		/// 
		/// </summary>
		public class BMGlyph
		{
			public int offsetX;
			public int offsetY;
			public int width;
			public int height;
			public int advance;
			public int lineHeight;
			public Vector2[] uv = new Vector2[4];
			public int channel;//0-n/a, 1-r,2-g,3-b,4-alpha
		}

		/// <summary>
		/// 
		/// </summary>
		public int size;

		/// <summary>
		/// 
		/// </summary>
		public bool resizable;

		Dictionary<int, BMGlyph> _dict;
		float scale;

		public BitmapFont(PackageItem item)
		{
			this.packageItem = item;
			this.name = UIPackage.URL_PREFIX + packageItem.owner.id + packageItem.id;
			this.canTint = true;
			this.canLight = false;
			this.canOutline = true;
			this.hasChannel = false;
			this.shader = ShaderConfig.bmFontShader;

			_dict = new Dictionary<int, BMGlyph>();
			this.scale = 1;
		}

		public void AddChar(char ch, BMGlyph glyph)
		{
			_dict[ch] = glyph;
		}

		override public void SetFormat(TextFormat format, float fontSizeScale)
		{
			if (resizable)
				this.scale = (float)format.size / size * fontSizeScale;
			else
				this.scale = fontSizeScale;
		}

		override public bool GetGlyphSize(char ch, out float width, out float height)
		{
			BMGlyph bg;
			if (ch == ' ')
			{
				width = Mathf.CeilToInt(size * scale / 2);
				height = Mathf.CeilToInt(size * scale);
				return true;
			}
			else if (_dict.TryGetValue((int)ch, out bg))
			{
				width = Mathf.CeilToInt(bg.advance * scale);
				height = Mathf.CeilToInt(bg.lineHeight * scale);
				return true;
			}
			else
			{
				width = 0;
				height = 0;
				return false;
			}
		}

		override public bool GetGlyph(char ch, ref GlyphInfo glyph)
		{
			BMGlyph bg;
			if (ch == ' ')
			{
				glyph.width = Mathf.CeilToInt(size * scale / 2);
				glyph.height = Mathf.CeilToInt(size * scale);
				glyph.vertMin = Vector2.zero;
				glyph.vertMax = Vector2.zero;
				glyph.uvBottomLeft = glyph.uvBottomRight = glyph.uvTopLeft = glyph.uvTopRight = Vector2.zero;
				glyph.channel = 0;
				return true;
			}
			else if (_dict.TryGetValue((int)ch, out bg))
			{
				glyph.width = Mathf.CeilToInt(bg.advance * scale);
				glyph.height = Mathf.CeilToInt(bg.lineHeight * scale);
				glyph.vertMin.x = bg.offsetX * scale;
				glyph.vertMin.y = bg.offsetY * scale;
				glyph.vertMax.x = (bg.offsetX + bg.width) * scale;
				glyph.vertMax.y = (bg.offsetY + bg.height) * scale;
				glyph.uvBottomLeft = bg.uv[0];
				glyph.uvTopLeft = bg.uv[1];
				glyph.uvTopRight = bg.uv[2];
				glyph.uvBottomRight = bg.uv[3];
				glyph.channel = bg.channel;
				return true;
			}
			else
				return false;
		}

		override public bool HasCharacter(char ch)
		{
			return ch == ' ' || _dict.ContainsKey((int)ch);
		}
	}
}
