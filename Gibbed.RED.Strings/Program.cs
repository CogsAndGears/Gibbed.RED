﻿/* Copyright (c) 2012 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using Gibbed.RED.FileFormats;
using NDesk.Options;

namespace Gibbed.RED.Strings
{
    internal class Program
    {
        private static string GetExecutableName()
        {
            return Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase);
        }

        public static void Main(string[] args)
        {
            var mode = Mode.Unknown;
            bool force = false;
            bool showHelp = false;

            var options = new OptionSet()
            {
                {
                    "d|decode",
                    "decode strings file",
                    v =>
                    {
                        if (v != null)
                        {
                            mode = Mode.Decode;
                        }
                    }
                    },
                {
                    "f|force",
                    "force decode strings file even when strings hash is not correct",
                    v => force = v != null
                    },
                {
                    "e|encode",
                    "encode strings file",
                    v =>
                    {
                        if (v != null)
                        {
                            mode = Mode.Encode;
                        }
                    }
                    },
                {
                    "h|help",
                    "show this message and exit",
                    v => showHelp = v != null
                    },
            };

            List<string> extras;

            try
            {
                extras = options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.Write("{0}: ", GetExecutableName());
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `{0} --help' for more information.", GetExecutableName());
                return;
            }

            if (showHelp == true ||
                mode == Mode.Unknown ||
                extras.Count < 1 ||
                extras.Count > 2)
            {
                Console.WriteLine("Usage: {0} [OPTIONS]+ input [output]", GetExecutableName());
                Console.WriteLine();
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (mode == Mode.Decode)
            {
                string inputPath = extras[0];
                string outputPath = extras.Count > 1 ? extras[1] : Path.ChangeExtension(inputPath, ".xml");

                using (var input = File.OpenRead(inputPath))
                {
                    var strings = new StringsFile();
                    strings.Deserialize(input);

                    if (strings.HasStringsHash == true &&
                        strings.StringsHashIsCorrect == false)
                    {
                        if (force == false)
                        {
                            Console.WriteLine("Error: strings hash mismatch! ({0} vs {1})",
                                              strings.StringsHash,
                                              strings.ComputedStringsHash);
                            return;
                        }

                        Console.WriteLine("Warning: strings hash mismatch! ({0} vs {1})",
                                          strings.StringsHash,
                                          strings.ComputedStringsHash);
                    }

                    var settings = new XmlWriterSettings()
                    {
                        Indent = true,
                        CheckCharacters = false,
                    };

                    using (var output = XmlWriter.Create(outputPath, settings))
                    {
                        output.WriteStartDocument();
                        output.WriteStartElement("strings");
                        output.WriteAttributeString("version", strings.Version.ToString(CultureInfo.InvariantCulture));
                        output.WriteAttributeString("encryption_key",
                                                    strings.EncryptionKey.ToString(CultureInfo.InvariantCulture));

                        output.WriteStartElement("keys");
                        foreach (var kv in strings.Keys)
                        {
                            output.WriteStartElement("key");
                            output.WriteAttributeString("id", kv.Key);
                            output.WriteValue(kv.Value);
                            output.WriteEndElement();
                        }
                        output.WriteEndElement();

                        output.WriteStartElement("texts");
                        foreach (var kv in strings.Texts)
                        {
                            output.WriteStartElement("text");
                            output.WriteAttributeString("id", kv.Key.ToString(CultureInfo.InvariantCulture));
                            output.WriteValue(kv.Value);
                            output.WriteEndElement();
                        }
                        output.WriteEndElement();

                        output.WriteEndElement();
                        output.WriteEndDocument();
                    }
                }
            }
            else if (mode == Mode.Encode)
            {
                string inputPath = extras[0];
                string outputPath = extras.Count > 1 ? extras[1] : Path.ChangeExtension(inputPath, ".w2strings");

                using (var input = File.OpenRead(inputPath))
                {
                    var doc = new XPathDocument(input);
                    var nav = doc.CreateNavigator();

                    var root = nav.SelectSingleNode("/strings");
                    if (root == null)
                    {
                        throw new FormatException();
                    }

                    var strings = new StringsFile
                    {
                        Version = uint.Parse(root.GetAttribute("version", "")),
                        EncryptionKey = uint.Parse(root.GetAttribute("encryption_key", "")),
                    };

                    var keys = root.Select("keys/key");
                    strings.Keys.Clear();
                    while (keys.MoveNext() == true)
                    {
                        if (keys.Current == null)
                        {
                            throw new InvalidOperationException();
                        }

                        strings.Keys.Add(
                            keys.Current.GetAttribute("id", ""),
                            uint.Parse(keys.Current.Value));
                    }

                    var texts = root.Select("texts/text");
                    strings.Texts.Clear();
                    while (texts.MoveNext() == true)
                    {
                        if (texts.Current == null)
                        {
                            throw new InvalidOperationException();
                        }

                        var value = texts.Current.Value;
                        value = value.Replace("\r\n", "\n");
                        value = value.Replace("\r", "");

                        strings.Texts.Add(
                            uint.Parse(texts.Current.GetAttribute("id", "")),
                            value);
                    }

                    using (var output = File.Create(outputPath))
                    {
                        strings.Serialize(output);
                    }
                }
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
