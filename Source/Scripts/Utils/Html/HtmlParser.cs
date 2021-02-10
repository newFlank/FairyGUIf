﻿using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FairyGUI.Utils
{
	/// <summary>
	/// 
	/// </summary>
	public class HtmlParser
	{
		public static HtmlParser inst = new HtmlParser();

		protected class TextFormat2 : TextFormat
		{
			public bool colorChanged;
		}

		protected List<TextFormat2> _textFormatStack;
		protected int _textFormatStackTop;
		protected TextFormat2 _format;
		protected List<HtmlElement> _elements;
		protected int _skipText;
		protected bool _ignoreWhiteSpace;
		protected HtmlParseOptions _defaultOptions;

		static List<string> sHelperList1 = new List<string>();
		static List<string> sHelperList2 = new List<string>();

		public HtmlParser()
		{
			_textFormatStack = new List<TextFormat2>();
			_format = new TextFormat2();
			_defaultOptions = new HtmlParseOptions();
		}

		virtual public void Parse(string aSource, TextFormat defaultFormat, List<HtmlElement> elements, HtmlParseOptions parseOptions)
		{
			if (parseOptions == null)
				parseOptions = _defaultOptions;

			_elements = elements;
			_textFormatStackTop = 0;
			_ignoreWhiteSpace = parseOptions.ignoreWhiteSpace;
			_format.CopyFrom(defaultFormat);
			_format.colorChanged = false;
			string text;

			XMLIterator.Begin(aSource, true);
			while (XMLIterator.NextTag())
			{
				if (_skipText == 0)
				{
					text = XMLIterator.GetText(_ignoreWhiteSpace);
					if (text.Length > 0)
						AppendText(text);
				}

				switch (XMLIterator.tagName)
				{
					case "b":
						if (XMLIterator.tagType == XMLTagType.Start)
						{
							PushTextFormat();
							_format.bold = true;
						}
						else
							PopTextFormat();
						break;

					case "i":
						if (XMLIterator.tagType == XMLTagType.Start)
						{
							PushTextFormat();
							_format.italic = true;
						}
						else
							PopTextFormat();
						break;

					case "u":
						if (XMLIterator.tagType == XMLTagType.Start)
						{
							PushTextFormat();
							_format.underline = true;
						}
						else
							PopTextFormat();
						break;

					case "sub":
						break;

					case "sup":
						break;

					case "font":
						if (XMLIterator.tagType == XMLTagType.Start)
						{
							PushTextFormat();

							_format.size = XMLIterator.GetAttributeInt("size", _format.size);
							string color = XMLIterator.GetAttribute("color");
							if (color != null)
							{
								string[] parts = color.Split(',');
								if (parts.Length == 1)
								{
									_format.color = ToolSet.ConvertFromHtmlColor(color);
									_format.gradientColor = null;
									_format.colorChanged = true;
								}
								else
								{
									if (_format.gradientColor == null)
										_format.gradientColor = new Color32[4];
									_format.gradientColor[0] = ToolSet.ConvertFromHtmlColor(parts[0]);
									_format.gradientColor[1] = ToolSet.ConvertFromHtmlColor(parts[1]);
									if (parts.Length > 2)
									{
										_format.gradientColor[2] = ToolSet.ConvertFromHtmlColor(parts[2]);
										if (parts.Length > 3)
											_format.gradientColor[3] = ToolSet.ConvertFromHtmlColor(parts[3]);
										else
											_format.gradientColor[3] = _format.gradientColor[2];
									}
									else
									{
										_format.gradientColor[2] = _format.gradientColor[0];
										_format.gradientColor[3] = _format.gradientColor[1];
									}
								}
							}
						}
						else if (XMLIterator.tagType == XMLTagType.End)
							PopTextFormat();
						break;

					case "br":
						AppendText("\n");
						break;

					case "img":
						if (XMLIterator.tagType == XMLTagType.Start || XMLIterator.tagType == XMLTagType.Void)
						{
							HtmlElement element = HtmlElement.GetElement(HtmlElementType.Image);
							element.FetchAttributes();
							element.name = element.GetString("name");
							_elements.Add(element);
						}
						break;

					case "a":
						if (XMLIterator.tagType == XMLTagType.Start)
						{
							PushTextFormat();

							_format.underline = _format.underline | parseOptions.linkUnderline;
							if (!_format.colorChanged && parseOptions.linkColor.a != 0)
								_format.color = parseOptions.linkColor;

							HtmlElement element = HtmlElement.GetElement(HtmlElementType.Link);
							element.FetchAttributes();
							element.name = element.GetString("name");
							_elements.Add(element);
						}
						else if (XMLIterator.tagType == XMLTagType.End)
						{
							PopTextFormat();

							HtmlElement element = HtmlElement.GetElement(HtmlElementType.LinkEnd);
							_elements.Add(element);
						}
						break;

					case "input":
						{
							HtmlElement element = HtmlElement.GetElement(HtmlElementType.Input);
							element.FetchAttributes();
							element.name = element.GetString("name");
							element.format.CopyFrom(_format);
							_elements.Add(element);
						}
						break;

					case "select":
						{
							if (XMLIterator.tagType == XMLTagType.Start || XMLIterator.tagType == XMLTagType.Void)
							{
								HtmlElement element = HtmlElement.GetElement(HtmlElementType.Select);
								element.FetchAttributes();
								if (XMLIterator.tagType == XMLTagType.Start)
								{
									sHelperList1.Clear();
									sHelperList2.Clear();
									while (XMLIterator.NextTag())
									{
										if (XMLIterator.tagName == "select")
											break;

										if (XMLIterator.tagName == "option")
										{
											if (XMLIterator.tagType == XMLTagType.Start || XMLIterator.tagType == XMLTagType.Void)
												sHelperList2.Add(XMLIterator.GetAttribute("value", string.Empty));
											else
												sHelperList1.Add(XMLIterator.GetText());
										}
									}
									element.Set("items", sHelperList1.ToArray());
									element.Set("values", sHelperList2.ToArray());
								}
								element.name = element.GetString("name");
								element.format.CopyFrom(_format);
								_elements.Add(element);
							}
						}
						break;

					case "p":
					case "ui":
					case "div":
					case "li":
						if (!IsNewLine())
							AppendText("\n");
						break;

					case "html":
					case "body":
						//full html
						_ignoreWhiteSpace = true;
						break;

					case "head":
					case "style":
					case "script":
					case "form":
						if (XMLIterator.tagType == XMLTagType.Start)
							_skipText++;
						else if (XMLIterator.tagType == XMLTagType.End)
							_skipText--;
						break;
				}
			}

			if (_skipText == 0)
			{
				text = XMLIterator.GetText(_ignoreWhiteSpace);
				if (text.Length > 0)
					AppendText(text);
			}

			_elements = null;
		}

		protected void PushTextFormat()
		{
			TextFormat2 tf;
			if (_textFormatStack.Count <= _textFormatStackTop)
			{
				tf = new TextFormat2();
				_textFormatStack.Add(tf);
			}
			else
				tf = _textFormatStack[_textFormatStackTop];
			tf.CopyFrom(_format);
			tf.colorChanged = _format.colorChanged;
			_textFormatStackTop++;
		}

		protected void PopTextFormat()
		{
			if (_textFormatStackTop > 0)
			{
				TextFormat2 tf = _textFormatStack[_textFormatStackTop - 1];
				_format.CopyFrom(tf);
				_format.colorChanged = tf.colorChanged;
				_textFormatStackTop--;
			}
		}

		protected bool IsNewLine()
		{
			if (_elements.Count > 0)
			{
				HtmlElement element = _elements[_elements.Count - 1];
				if (element != null && element.type == HtmlElementType.Text)
					return element.text.EndsWith("\n");
				else
					return false;
			}

			return true;
		}

		protected void AppendText(string text)
		{
			HtmlElement element;
			if (_elements.Count > 0)
			{
				element = _elements[_elements.Count - 1];
				if (element.type == HtmlElementType.Text && element.format.EqualStyle(_format))
				{
					element.text += text;
					return;
				}
			}

			element = HtmlElement.GetElement(HtmlElementType.Text);
			element.text = text;
			element.format.CopyFrom(_format);
			_elements.Add(element);
		}
	}
}
