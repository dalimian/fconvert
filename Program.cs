using System;
using System.Collections.Generic;
using System.Text;
using href.Utils;
using System.IO;
namespace fconvert
{
    class Program
    {
        #region helpers

        private static bool IsOneofOftenMisplacedEncodings(string name, byte[] sourceBytes)
        {
            if (name == "windows-874" || name ==  "big5" || name == "ks_c_5601-1987" || name == "iso-2022-jp" || name == "gb2312" || name == "ibm852"
                && (InvalidCharCount(Encoding.GetEncoding(1252),sourceBytes) == 0 || InvalidCharCount(Encoding.GetEncoding("utf-8"),sourceBytes) == 0 )
                )
            {
                return true;
            }
            return false;
        }
        private static Encoding DecideFinalEncoding(byte[] sourceBytes, Encoding[] detectedEncodingsForAllBytes, string filePath, List<Encoding> allEncodingsset_intersection, List<string> onlyEncodingList, string encOnInx0Most, string reportStr, out string message)
        {
            Encoding encodingToUse = null;
            message = "";
            if (onlyEncodingList.Count > 0)
            {
                string name = MostRepeatedStr(onlyEncodingList.ToArray());
                if (InvalidCharCount(Encoding.GetEncoding(name), sourceBytes) == 0 && !IsOneofOftenMisplacedEncodings(name,sourceBytes))
                    return encodingToUse = Encoding.GetEncoding(name);

            }

            if (detectedEncodingsForAllBytes.Length == 1 && InvalidCharCount(detectedEncodingsForAllBytes[0], sourceBytes) == 0)
            {
                return encodingToUse = detectedEncodingsForAllBytes[0];
            }


            if ((encOnInx0Most == "utf-8") && InvalidCharCount(Encoding.UTF8, sourceBytes) == 0)
            {
                return encodingToUse = Encoding.UTF8;
            }
            /// see if utf-8 is both in all bytes and set_intersection
            /// 
            bool utfInAllBytes = false;
            for (int i = 0; i < detectedEncodingsForAllBytes.Length; i++)
            {
                if (detectedEncodingsForAllBytes[i].HeaderName == "utf-8")
                    utfInAllBytes = true;
            }
            bool utfInset_intersection = false;
            for (int i = 0; i < allEncodingsset_intersection.Count; i++)
            {
                if (allEncodingsset_intersection[i].HeaderName == "utf-8")
                    utfInset_intersection = true;
            }
            if (utfInAllBytes && utfInset_intersection && InvalidCharCount(Encoding.UTF8, sourceBytes) == 0) // utf was not in only but it is in all and set_intersection, that should be enough to assume utf
                return encodingToUse = Encoding.UTF8;

            /// if detected on the whole file, no invalid chars, the encoding is also in interesection
            /// 
            for (int i = 0; i < detectedEncodingsForAllBytes.Length; i++)
            {
                if (IsOneofOftenMisplacedEncodings(detectedEncodingsForAllBytes[i].HeaderName, sourceBytes))
                    continue;
                /// decide if all[i] is in set_intersection...
                /// 
                foreach (Encoding he in allEncodingsset_intersection)
                {
                    if (he.HeaderName == detectedEncodingsForAllBytes[i].HeaderName)
                    {
                        if (InvalidCharCount(he, sourceBytes) > 0)
                            continue;

                        return encodingToUse = detectedEncodingsForAllBytes[i];

                    }
                }
            }// none of all[i] was in set_intersection


            if (allEncodingsset_intersection.Count == 0) // set_intersection is EMPTY. at this point we know the onlyEncoding is empty too(cause thats the first check)
            {
                foreach (Encoding en in detectedEncodingsForAllBytes)
                {
                    if (IsOneofOftenMisplacedEncodings(en.HeaderName,sourceBytes))
                        continue;

                    if (InvalidCharCount(en, sourceBytes) == 0)
                        return encodingToUse = en;
                }
                message = string.Format("{0}: set_intersection is empty. details: \n\n{1}",
                    filePath, reportStr);

            }
            // since none of all[i] was in set_intersection
            string nm = allEncodingsset_intersection[0].HeaderName;
            if (!IsOneofOftenMisplacedEncodings(nm,sourceBytes) &&
                InvalidCharCount(allEncodingsset_intersection[0], sourceBytes) == 0)
                return encodingToUse = allEncodingsset_intersection[0];
            else
            {
                foreach (Encoding en in detectedEncodingsForAllBytes)
                {
                    if (IsOneofOftenMisplacedEncodings(en.HeaderName,sourceBytes))
                        continue;

                    if (InvalidCharCount(en, sourceBytes) == 0)
                        return encodingToUse = en;
                }

            }


            return encodingToUse;
        }

        static bool ElementIsInList(Encoding i_element, Encoding[] i_list)
        {
            foreach (Encoding e in i_list)
            {
                if (e.HeaderName == i_element.HeaderName)
                    return true;
            }

            return false;
        }

        static int SubStrCount(string i_str, string i_subStr)
        {
            int count = 0;
            int i = 0;
            while (i < i_str.Length)
            {
                if (i_str[i] == i_subStr[0])
                {
                    bool wordMatch = true;
                    int j;
                    for (j = 1; j < i_subStr.Length; j++)
                    {
                        if ((i + j) >= i_str.Length)
                        {
                            wordMatch = false;
                            break;
                        }
                        if (i_subStr[j] != i_str[i + j])
                        {
                            wordMatch = false;
                            break;
                        }
                    }
                    count += wordMatch == true ? 1 : 0;
                    i = i + j;
                    continue;
                }
                i++;
            }
            return count;

        }

        public static string MostRepeatedStr(string[] i_l)
        {
            Dictionary<string, int> d = new Dictionary<string, int>();
            foreach (string s in i_l)
            {
                if (!d.ContainsKey(s))
                    d.Add(s, 1);
                else
                    d[s]++;
            }
            string mostRepeatedName = null;
            int count = 0;

            foreach (string key in d.Keys)
            {
                if (mostRepeatedName == null)
                {
                    mostRepeatedName = key;
                    count = d[key];

                }
                else
                    if (d[key] > count)
                    {
                        mostRepeatedName = key;
                        count = d[key];
                    }
            }
            return mostRepeatedName;

        }

        static int InvalidCharCount(Encoding e, byte[] array)
        {


            if (e.BodyName.Contains("iso-8859-1")) // latin 1
            {
                /// invalid latin-1...
                int invalidLatinChars = 0;
                for (int invalidLatinIndex = 0; invalidLatinIndex < array.Length; invalidLatinIndex++)
                {
                    if (array[invalidLatinIndex] == 10 || array[invalidLatinIndex] == 13)
                        continue;

                    if ((array[invalidLatinIndex] >= 14 && array[invalidLatinIndex] <= 31) || (array[invalidLatinIndex] >= 127 && array[invalidLatinIndex] <= 159))
                        invalidLatinChars++;
                }
                return invalidLatinChars;
            }
            else
            {
                e = Encoding.GetEncoding(e.HeaderName,
                                            new EncoderReplacementFallback("[error]"), new DecoderReplacementFallback("[error]"));

                int invalidChars = 0;
                string s = e.GetString(array);
                for (int i = 0; i < s.Length; i++)
                {
                    if ((s[i] == '\uFFFD'))
                        invalidChars++;
                }
                invalidChars += SubStrCount(s, ((DecoderReplacementFallback)e.DecoderFallback).DefaultString);

                return invalidChars;
            }

        }

        #endregion

        static void Main(string[] args)
        {


            string help =
            #region help str
 @"
The Problem
when you download the database in text format (text files) each file can be of different encoding- the encoding they used to write the content of the file can be different from file to file. Moreover, these files don’t have BOM (Byte Order Mark) indicator so there is no way to reliably read the encoding of the content.

Most usually the encodings of the free db files are either ASCII, Latin-1, or utf-8. These encodings is what free db claims to support, and they do a fair share of validations to not accept data that is invalid for any of these encodings. But these validations are not 100%. For example lot of the files for Russian discs are in Windows Cyrillic (CP 1251), these files slip through the free db validations cause most valid text in this encoding is also valid latin-1.  The reason being is that first they both are 8-bit encodings, the character codes 192-255 define the whole Russian alphabet (capital and small letter), and codes 192 through 255 are also perfectly valid latin-1 char codes. In certain situations a valid utf-8 is also a valid latin-1 though as the strings get large this happens less. So these encodings is what I have encountered in freedb, god only knows what other encodings there are.

The ultimate goal today for any database and/or service dealing with multiple languages is for all data to be in some kind of Unicode encoding (most popular and supported by all browsers is utf-8, next is utf-16).  Otherwise it becomes a nightmare. So the problem is how to end up with database in only  one Unicode encoding that would support all languages from the input of bunch of files from bunch of different encodings.



Solution 
I wrote a .Net console program that detects the original encoding and converts all files to one destination unicode encoding (utf-8, utf-16). My program uses a nice .net wrapper EncodingTools.dll written by Carsten Zeumer. You can look at the description and downlad the source at http://www.codeproject.com/KB/recipes/DetectEncoding.aspx. This wrapper internally uses IMultiLang2 interface from MLang.dll – this dll used by internet explorer 5.0 (and on I think) to detect encodings of text. This detection was fairly accurate but I made some optimizations in my program to make it lot more accurate on free db data. I optimized it to work best for utf-8, and latin-1 since these are the core encodings of free db. After these optimizations the accuracy was about 95% on a set of 100 random files I tested it on. 


Summary:
    This utility converts all non-ascii files under a folder you 
    specified by -root param to encoding specified by -dstEncoding 
    parameter.

    All files in -root and its subdirectories are analyzed. If the file is
    ASCII it is untouched. If it is non-ascii the utility attempts to detect
    the encoding of the content (i.e., latin-1, Windows Cyrillic, utf-8, etc.)
    and converts it to encoding specified by -dstEncoding. The converted file 
    is moved to automatically generated -root/converted folder. 

    encoding detection was fine tuned with only data files from free db in 
    mind. Running this util on other files will (most probably) yield 
    inacurate results. If you need to use it on other files leave the 
    skeleton of this program and rewrite your own encoding detection 
    logic. 

example Usage:
    >fconvert.exe -dstEncoding utf-8 -root G:\work\freedb\extracted -lf freedb-parse.log -errors freedb-errors.log -log 1 -start 0 -end -1
    
    in this example all non-ascii files and files under subdirectories of 
    G:\work\freedb\extracted will be converted to utf-8 and moved to 
    G:\work\freedb\extracted.converted. The originals will be saved in 
    G:\work\freedb\extracted.backup before they are converted. after the
    utility completes you will end up having only ascii files left in
    G:\work\freedb\extracted, and all utf-8 files in 
    G:\work\freedb\extracted\converted. Same subdirectory structure
    is preserved for the converted folder as in root.


required params:
    -r or
    -root <folderName> -- root folder name.
    ex: g:\work\freedb\freedb-complete-20090701.extracted
    folder that has original data to be parsed (everything including 
    subfolders will be parsed recursively
	\data
	\folk
	\jazz
	\rock
	...
    
    -lf or    	
    -logFile <fileName> -- name of file to log to.
    ex: g:\work\freedb\freedb-complete-20090701.extracted_1.log
    logs will appended to the end of the specified file. if file 
    does not exist it will be created. The contents of the file
    is in Unicode (Code Page 1200) so instruct this to your
    editor so you see the contents correctly. The contents of 
    the log will list the which encoding and why was decided by
    the program to be the most likely original encoding. More
    about this in developer notes further bolow


optional params:
    -df or
    -destFolder <folder name> -- converted files will be moved
    to this folder.
    ex: g:\work\freedb\freedb-complete-20090701.extracted.converted
    so after the utility completes the -root will have only ascii
    files and this folder will have all the non-ascii files. if the
    folder does not exist it will be created. If this param is not
    specified the files will be written in a newly created directory 
    with the same name as -root and the extension .converted. Same 
    subdirectory structure is preserved for this folder as in root
    

    -backupFolder or
    -backup <folder name> -- name of backup folder
    ex: g:\work\freedb\freedb-complete-20090701.extracted.backup
	folder to contain original files before they are converted and 
    moved to converted folder. if the directory does not exist it 
    will be created. If this param is not specified the files will 
    be written in a newly created directory with the same name as 
    -root and the extension .backup. Same subdirectory structure 
    is preserved for this folder as in root

    -log <0|1> -- do log or not. default is 1. If you have more
    than a few thousand files your log file will be huge so you
    can turn logging off to speed up the process. The log file
    is in unicode (utf-16 CP 1200) format. You need to specify this
    to the editor when opening the file otherwise you see jibberish.

    -start <int> -- 0 based start index. default is 0 which means
    the process starts from the beginning - first file.

    -end <pos int|-1> -- 0 based end index. if not specified, goes 
    to end of the list. If -1 goes to end of list. default is -1.
    
    [-dst or
     -dstEncoding <encodingName|code page>] -- destination encoding.
    specifies destination encoding to convert the files to. either 
    specify an encoding name or code page number. if specified a string 
    i will call Encoding.GetEncoding(string), otherwise i will call 
    overloaded Encoding.GetEncoding(int). default is 'utf-8'. 

    -doBackup <0|1> -- specifies if the files should or should not
    move to the back folder. If 0 the original file will remain in
    the root folder, if 1 the original file is moved to the folder 
    specified by backup(thus is deleted from root). Default is 1.

    -destOverwrite <0|1> -- If 0 the converted file is overwritten
    (if the -converted folder already contains the same file) to the
    -converted folder. If 1 and the file to be processed is already
    in -converted the file is not processed. Default is 0.
    
 
";
            #endregion



            #region parse the CMD args

            string root = "";
            string backupFolder = "";
            string convertedFolder = "";
            string logFile = "";
            string errorsFile = "";
            int doLog = 1;
            string dstEncodingStr = "utf-8";
            Encoding dstEncoding = Encoding.UTF8;
            int startIndex = 0;
            int endIndex = -1;
            int doBackup = 1;
            int destOverwrite = 0;


            if (args.Length < 6)
            {
                Console.WriteLine(help);
                return;
            }

            // -root G:\work\freedb\freedb-update-20090701-20090801.tar.bz2.extracted

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-root":
                    case "-r":
                        root = args[++i];
                        break;
                    case "-backup":
                    case "-backupFolder":
                        backupFolder = args[++i];
                        break;
                    case "-df":
                    case "-destFolder":
                        convertedFolder = args[++i];
                        break;

                    case "-lf":
                    case "-logFile":
                        logFile = args[++i];
                        break;
                    case "-e":
                    case "-errors":
                        errorsFile = args[++i];
                        break;

                    case "-start":
                        startIndex = System.Convert.ToInt32(args[++i]);
                        break;
                    case "-end":
                        endIndex = System.Convert.ToInt32(args[++i]);
                        break;
                    case "-log":
                        doLog = System.Convert.ToInt32(args[++i]);
                        break;
                    case "-dst":
                    case "-dstEncoding":
                        int cp;
                        try
                        {
                            dstEncodingStr = args[++i];
                            cp = System.Convert.ToInt32(dstEncodingStr);
                            dstEncoding = Encoding.GetEncoding(cp);
                        }
                        catch (FormatException fe)
                        {
                            dstEncoding = Encoding.GetEncoding(dstEncodingStr);
                        }
                        break; 
                    case "-doBackup":
                        doBackup = System.Convert.ToInt32(args[++i]);
                        break;
                    case "-destOverwrite":
                        destOverwrite = System.Convert.ToInt32(args[++i]);
                        break;

                    default:
                        Console.WriteLine("Unrecognised argument {0}", args[i]);
                        return;




                }
            }

            if (backupFolder == null || backupFolder == string.Empty)
                backupFolder = root + ".backup";
            if (convertedFolder == null || convertedFolder == string.Empty)
                convertedFolder = root + ".converted";
            if (errorsFile == null || errorsFile == string.Empty)
                errorsFile = root + ".errors.log";
            if (logFile == null || logFile == string.Empty)
                logFile = root + ".log";


            if (!Directory.Exists(root))
            {
                Console.WriteLine("specified root directory {0} does not exist", root);
                return;
            }
            if (!Directory.Exists(convertedFolder))
            {
                Directory.CreateDirectory(convertedFolder);
                Console.WriteLine("{0} (destination folder) does not exist. Press Any Key to Create, Cntrl+C to abort...", convertedFolder);
                Console.ReadLine();
            }
            if (!Directory.Exists(backupFolder))
            {
                Directory.CreateDirectory(backupFolder);
                Console.WriteLine("{0} (backup folder) does not exist. Press Any Key to create, Contrl+C to abort...", backupFolder);
                Console.ReadLine();

            }

            #endregion


            

            DateTime timeStart = DateTime.Now;

            Console.Write("Reading File Names...");
            string[] filePaths = System.IO.Directory.GetFiles(root, "*.*", System.IO.SearchOption.AllDirectories);
            int count = filePaths.Length;
            Console.WriteLine("done.");

            int errorCount = 0;
            int skippedAscii = 0;
            int skippedConverted = 0;
            int processedCount = 0;

            Encoding[] detectedEncodingsForAllBytes;



            for (int fi = startIndex; fi <= (endIndex == -1 ? filePaths.Length - 1 : endIndex); fi++)
            {
                string filePath = filePaths[fi];

                Console.Write("{0,7} / {1} {2} ...", fi, filePaths.Length - 1, filePath);


                string[] splittedPath = filePath.Split(new char[] { '\\', '/' });

                string[] splittedRoot = root.Split(new char[] { '\\', '/' });

                string[] relPath = new string[splittedPath.Length - splittedRoot.Length];
                
                Array.Copy(splittedPath,
                            splittedRoot.Length, // source start index
                            relPath,
                            0,
                            splittedPath.Length - splittedRoot.Length);


                string backupPath = backupFolder + "\\" + string.Join("\\",relPath);
                string convertedPath = convertedFolder + "\\" + string.Join("\\", relPath);

                if (destOverwrite == 0)
                    if (File.Exists(convertedPath) )
                    {
                        Console.WriteLine(" skipped (already converted).");
                        skippedConverted++;
                        continue;
                    }






                string s = System.IO.File.ReadAllText(filePath, Encoding.GetEncoding(850)); // dos encoding 850 has 0-255 defined
                if (EncodingTools.IsAscii(s) == true)
                {
                    Console.WriteLine(" (ascii).");
                    ++skippedAscii;
                    continue;
                }



                byte[] sourceBytes = System.IO.File.ReadAllBytes(filePath); // "C:/work/utf-8/14108c15" -utf

                /// invalid latin-1...
                int invalidLatinChars = InvalidCharCount(Encoding.GetEncoding(1252), sourceBytes);

                /// encodings for all bytes...
                /// 
                List<Encoding> preffered = new List<Encoding>();
                try
                {
                    preffered.AddRange(EncodingTools.DetectInputCodepages(sourceBytes, 10));
                }
                catch (Exception ex)
                {
                    // every once in a while DetectInputCodepages() throws
                }
                preffered.Add(Encoding.UTF8);
                preffered.Add(Encoding.GetEncoding(1252));

                detectedEncodingsForAllBytes = preffered.ToArray();

                for (int j = 0; j < detectedEncodingsForAllBytes.Length; j++)
                {
                    Encoding replacement = Encoding.GetEncoding(detectedEncodingsForAllBytes[j].CodePage,
                        new EncoderReplacementFallback("[error]"),
                        new DecoderReplacementFallback("[error]"));
                    detectedEncodingsForAllBytes[j] = replacement;
                }

                string set_intersectionStr = "";


                /// individual lines...
                byte[] byteNameArray;
                List<byte> byteNameList = new List<byte>();
                string allEncodingsForEachLineStr = "detections per individual (non-ascii) lines (sometimes these are more acurate): \n";
                List<Encoding[]> allEncodingsforEachLine = new List<Encoding[]>();
                List<Encoding> allEncodingsset_intersection = new List<Encoding>();
                string onlyEncoding = "";
                List<string> onlyEncodingList = new List<string>();
                bool track = false;
                for (int i = 0; i < sourceBytes.Length; i++)
                {
                    if (sourceBytes[i] == 61)
                    {
                        track = true;
                        continue;
                    }
                    if (track == false)
                        continue;
                    if (sourceBytes[i] == 13 || sourceBytes[i] == 10) // \r
                    {
                        if (byteNameList.Count > 0) // we got a name
                        {
                            byteNameArray = byteNameList.ToArray();
                            Encoding likelyEncoding;
                            Encoding[] likelyEncodings;
                            try
                            {
                                likelyEncoding = EncodingTools.DetectInputCodepage(byteNameArray);

                                likelyEncodings = EncodingTools.DetectInputCodepages(byteNameArray, 10);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                                byteNameList.Clear();
                                track = false;
                                continue;
                            }
                            /// set_intersection prep...
                            /// 
                            if (!EncodingTools.IsAscii(Encoding.GetEncoding(850).GetString(byteNameArray)))
                            {
                                List<Encoding> likelyEncodingsAsciiStripped = new List<Encoding>();
                                foreach (Encoding e in likelyEncodings)
                                {
                                    if (e.CodePage != 1252) // not latin-1
                                    {
                                        // sometimes its really 1252 but it detects it as 1254 which decodes to same string
                                        if (e.GetString(byteNameArray) == Encoding.GetEncoding(1252).GetString(byteNameArray)) // but equivalent to latin-1
                                            likelyEncodingsAsciiStripped.Add(Encoding.GetEncoding(1252));
                                    }
                                    if (!e.HeaderName.Contains("scii") && InvalidCharCount(e, byteNameArray) == 0)
                                        likelyEncodingsAsciiStripped.Add(e);

                                }
                                if (likelyEncodingsAsciiStripped.Count > 0)
                                    allEncodingsforEachLine.Add(likelyEncodingsAsciiStripped.ToArray());
                            }

                            /// only encoding...
                            /// 
                            if (likelyEncodings.Length == 1 && likelyEncodings[0].HeaderName.Contains("scii") == false)
                            {
                                if (likelyEncodings[0].CodePage != 1252)
                                {
                                    if (likelyEncodings[0].GetString(byteNameArray) != Encoding.GetEncoding(1252).GetString(byteNameArray))
                                    {
                                        if (InvalidCharCount(likelyEncodings[0], byteNameArray) == 0)
                                        {
                                            onlyEncoding += " " + likelyEncodings[0].HeaderName;
                                            onlyEncodingList.Add(likelyEncodings[0].HeaderName);
                                        }
                                    }
                                }
                                else // 1252 only
                                {
                                    onlyEncoding += " " + likelyEncodings[0].HeaderName;
                                    onlyEncodingList.Add(likelyEncodings[0].HeaderName);
                                }
                            }

                            /// decode and log...
                            /// 
                            if (!EncodingTools.IsAscii(Encoding.GetEncoding(850).GetString(byteNameArray)))
                            {
                                for (int j = 0; j < likelyEncodings.Length; j++)
                                {

                                    string bytesString = "  ";
                                    foreach (byte byteN in byteNameArray)
                                        bytesString += " " + byteN.ToString(); // print the bytes

                                    Encoding en = Encoding.GetEncoding(likelyEncodings[j].HeaderName, new EncoderReplacementFallback("[error]"), new DecoderReplacementFallback("[error]"));
                                    allEncodingsForEachLineStr += String.Format("\n{0}: {1,13}  {2}  {3}",
                                        j, en.HeaderName,
                                        en.GetString(byteNameArray),
                                        bytesString);

                                    if (en.CodePage != 1252 && en.GetString(byteNameArray) == Encoding.GetEncoding(1252).GetString(byteNameArray))
                                    {
                                        allEncodingsForEachLineStr += String.Format("\n : {0,13}  {1}  {2}",
                                            Encoding.GetEncoding(1252).HeaderName,
                                            Encoding.GetEncoding(1252).GetString(byteNameArray),
                                            bytesString);
                                    }
                                }
                            }

                        }
                        track = false;
                        byteNameList.Clear();
                        continue;
                    }
                    byteNameList.Add(sourceBytes[i]);
                } // got the encodings for seperate lines. Now calc the set_intersection...
                string logStr;

                if (allEncodingsforEachLine.Count == 0)
                {
                    logStr = string.Format("\n\n\n-----------------------------------------------\n{0}\nskipped: all artist entries were ascii (even though file was non ascii)",
                        filePath);

                    if (doLog != 0)
                        File.AppendAllText(logFile, logStr);
                    Console.WriteLine(" (ascii).");
                    skippedAscii++;
                    continue;

                }
                ///set_intersection...
                ///
                allEncodingsset_intersection.AddRange(allEncodingsforEachLine[0]);

                List<string> encodingsOfIndex0forEachLine = new List<string>();
                encodingsOfIndex0forEachLine.Add(allEncodingsforEachLine[0][0].HeaderName);

                for (int i = 1; i < allEncodingsforEachLine.Count; i++)
                {
                    encodingsOfIndex0forEachLine.Add(allEncodingsforEachLine[i][0].HeaderName);
                    for (int j = 0; j < allEncodingsset_intersection.Count; j++)
                    {
                        if (!ElementIsInList(allEncodingsset_intersection[j], allEncodingsforEachLine[i]))
                        {
                            // set_intersection element is not contained in current line elements... shouldnt be in set_intersection. delete the set_intersection element
                            allEncodingsset_intersection.RemoveAt(j);
                            --j;
                        }
                    }

                }
                ///set_intersection has the correct set_intersection now
                set_intersectionStr = "\nset_intersection elements  " + allEncodingsset_intersection.Count + ":";
                for (int i = 0; i < allEncodingsset_intersection.Count; i++)
                {
                    set_intersectionStr += "\n" + i + ": " + allEncodingsset_intersection[i].HeaderName;
                }

                ///build str of encodings detected for the file as a whole
                ///
                string detectedEncodingsForAllBytesStr = "\ndetectedEncodingsForAllBytes  " + detectedEncodingsForAllBytes.Length + ":";
                for (int i = 0; i < detectedEncodingsForAllBytes.Length; i++)
                {
                    detectedEncodingsForAllBytesStr += "\n" + i + ": " + detectedEncodingsForAllBytes[i].HeaderName + "  invalid char count: " + InvalidCharCount(detectedEncodingsForAllBytes[i], sourceBytes);
                }

                string encOnInx0Most = MostRepeatedStr(encodingsOfIndex0forEachLine.ToArray());

                string reportStr = String.Format("{0}\n\nInvalid Latin :{1}\n{2}\n\nencoding that was only encoding: \'{3}\'\n\nEncoding on index 0 most: {4}\n\n{5}",
                    detectedEncodingsForAllBytesStr, invalidLatinChars, set_intersectionStr, onlyEncoding, encOnInx0Most, allEncodingsForEachLineStr);


                /// decide ultimate encoding...
                /// 
                string errorMsg;
                Encoding detectedSrcEncoding = DecideFinalEncoding(sourceBytes, detectedEncodingsForAllBytes, filePath, allEncodingsset_intersection, onlyEncodingList, encOnInx0Most, reportStr, out errorMsg);

                if (detectedSrcEncoding == null || InvalidCharCount(detectedSrcEncoding, sourceBytes) > 0) // error
                {
                    logStr = string.Format("\n\n\n\n\n\n\n\n-------------------------------------------\n{0} / {1} {2}\n\nCould not determine source encoding:\n   {3}.\n\ndetails:\n{4}",
                       fi, filePaths.Length, filePath, errorMsg, reportStr);
                    File.AppendAllText(errorsFile, logStr, Encoding.Unicode);
                    errorCount++;
                    Console.WriteLine(" error.");
                    continue;
                }

                int toUseInvalidCharCount = InvalidCharCount(detectedSrcEncoding, sourceBytes);

                logStr = string.Format("\n\n\n\n\n\n\n\n-------------------------------------------\n{0} / {1} {2}\n\nencoding used:\n   {3}(CP: {4}) Invalid_char:{5}.\n{6}",
                   fi, filePaths.Length, filePath, detectedSrcEncoding.HeaderName, detectedSrcEncoding.CodePage, toUseInvalidCharCount, reportStr);

                if (doLog != 0)
                    File.AppendAllText(logFile, logStr, Encoding.Unicode);




                /// convert
                /// 

                byte[] dstBytes = System.Text.Encoding.Convert(detectedSrcEncoding, dstEncoding,
                                                                sourceBytes);
                string dirName = convertedPath.Substring(0, convertedPath.LastIndexOf('\\'));
                if (!Directory.Exists(dirName))
                    Directory.CreateDirectory(dirName);
                File.WriteAllBytes(convertedPath, dstBytes);

                if (doBackup != 0)
                {

                    if (!File.Exists(backupPath))
                    {
                        string dirNameB = backupPath.Substring(0, backupPath.LastIndexOf('\\'));
                        if (!Directory.Exists(dirNameB))
                            Directory.CreateDirectory(dirNameB);

                        File.Move(filePath, backupPath);
                    }
                    
                }
                processedCount++;
                Console.WriteLine(" converted.");

            }
            /// done processing all files
            /// 
            DateTime timeEnd = DateTime.Now;
            TimeSpan span = timeEnd - timeStart;

            Console.WriteLine("\nDone. {0}\nTotal: {1}. untouched (prev converted):{2}.  untouched (ascii): {3}.  Converted: {4}.  Errors: {5}.\n\npress any key to continue.",
                span.ToString(), filePaths.Length, skippedConverted, skippedAscii, processedCount, errorCount);


            Console.Read();


        }


    }
}
