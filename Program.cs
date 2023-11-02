using System.Runtime.CompilerServices;

public class Program {
    public static void Main(string[] args) {
        // Disable GC, we don't actually need to GC while running this. Fuck memory.
        // Reserve 1GB for this
        var success = GC.TryStartNoGCRegion(1 * 1024 * 1024 * 1024L);


        string inputFile;
        var name = args[0];
        inputFile = name + ".in";

        // please don't give me a wrong input file, this program will cry

        var contents = File.ReadAllBytes(inputFile);

        // The proper solution is not leaking memory. However, who cares, the program only runs once... the OS can clean it up anyway
        //if (success) {
        //    GC.EndNoGCRegion();
        //}


        var numItems = readIntUntilComma(new MemoryStream(contents));
        // only read the beginning, connOffset is the index of the first connection data
        var records = byteSplit(contents, 1 + numItems * 2, out var connOffset);
#if DEBUG
        Console.Out.WriteLine(numItems);
        Console.Out.WriteLine(connOffset);
#endif
        // where to start reading
        var baseOffset = 1;
        for (int i = 0; i < numItems; i++) {
            var theItem = i * 2;
            var x = records[baseOffset + theItem];
            var y = records[baseOffset + theItem + 1];
            homemadeVeryUnsafeIntParse(x, out var ix);
            homemadeVeryUnsafeIntParse(y, out var iy);
            // do something else as well
        }

        // From here, we don't split at all but we do byte magic instead.
        // value = connOffset + i * 2 (as we skip commas)
        for (int i = 0; i < numItems; i++) {
            for (int j = 0; j < numItems; j++) {
                var val = (contents[connOffset + (i * numItems + j) * 2]);
                var value = homemadeUnsafeIntParse(val);
                if (value == 1) {
                    // well, we do something here!
                }
            }
        }
    }

    // only 6 numbers long! we are lazy
    private static List<char[]> byteSplit(byte[] contents) {
        var arrs = new List<char[]>();
        var arr = new char[6];
        var idx = 0;
        for (int i = 0; i < contents.Length; i++) {
            switch (contents[i]) {
                // ascii ,
                case 44:
                    arrs.Add(arr);
                    arr = new char[6];
                    idx = 0;
                    break;
                default:
                    arr[idx++] = (char)contents[i];
                    break;
            }
        }

        // need to add the last one!!
        arrs.Add(arr);

        return arrs;
    }

    // only 4 numbers long! we are lazy
    // A trained monkey could speed this up with SIMD. Alas, I am an untrained monkey
    private static List<char[]> byteSplit(byte[] contents, int max, out int finalPos) {
        var arrs = new List<char[]>();
        var arr = new char[4];
        var idx = 0;
        var num = 0;
        for (int i = 0; i < contents.Length; i++) {
            // if we reach the number of records, stop
            if (num >= max) {
                finalPos = i;
                return arrs;
            }

            switch (contents[i]) {
                // ascii ,
                case 44:
                    arrs.Add(arr);
                    arr = new char[4];
                    idx = 0;
                    num++;
                    break;
                default:
                    arr[idx++] = (char)contents[i];
                    break;
            }
        }

        // need to add the last one!!
        arrs.Add(arr);
        finalPos = contents.Length - 1;
        return arrs;
    }

    // holy fuck this code is awful
    public static int readIntUntilComma(Stream reader) {
        char[] number = new char[4];
        int pos = 0;
        char ch;
        do {
            ch = (char)reader.ReadByte();
            switch (ch) {
                case ',': {
                    homemadeVeryUnsafeIntParse(number, out var ret);
                    return ret;
                }
                default: {
                    number[pos++] = ch;
                    break;
                }
            }
        } while (ch != ',');

        return 0;
    }

    // https://codereview.stackexchange.com/questions/200935/custom-integer-parser-optimized-for-performance
    /// <summary>High performance integer parser with rudimentary flexibility.</summary>
    /// <remarks>Doesn't support negative numbers, and no whitespace or other non-digit characters
    /// may be present.
    /// Will not parse strings with more than 10 numeric characters,
    /// even if there are leading zeros (such that the integer does not overflow).</remarks>
    public static unsafe bool homemadeVeryUnsafeIntParse(char[] input, out int result) {
        // We never expect null, but enforcing this may enable some JIT optimizations.
        if (input == null) {
            result = default;
            return false;
        }

        fixed (char* cString = input) {
            unchecked {
                char* nextChar = cString;
                /*bool isNegative = false;
                // Check whether the first character is numeric
                if (*nextChar < '0' || *nextChar > '9') {
                    // Only allow a negative sign at the beginning of the string.
                    if (*nextChar != '-') {
                        result = default;
                        return false;
                    }

                    isNegative = true;
                    // Any other non-numeric characters after this is an error.
                    if (*++nextChar < '0' || *nextChar > '9') {
                        result = default;
                        // Special Case: Excel has been known to format zero as "-".
                        // So return true here IFF this non-digit char is the end-of-string.
                        return *nextChar == Char.MinValue;
                    }
                }*/

                // Now process each character of the string
                int localValue = *nextChar++ - '0';
                while (*nextChar >= '0' && *nextChar <= '9')
                    localValue = localValue * 10 + (*nextChar++ - '0');
                // If the non-numeric character encountered to end the while loop
                // wasn't the null terminator, the string is invalid.
                if (*nextChar != Char.MinValue) {
                    result = default;
                    return false;
                }

                // We need to check for an integer overflow based on the length of the string
                long ptrLen = nextChar - cString;
                // Result and overflow logic is different if there was a minus sign.
                /*if (isNegative) {
                    result = -localValue;
                    // Longest possible negative int is 11 chars (-2147483648)
                    // Less than 11 characters (including negative) is no risk of overflow
                    if (ptrLen < 11L) return true;
                    // More than 11 characters is definitely an overflow.
                    if (ptrLen > 11L) return false;
                    // Exactly 11 characters needs to be checked for overflow.
                    // Neat Trick: An overflow will always result in the first digit changing.
                    return *(cString + 1) - '0' == localValue / 1000000000
                           // Special case, parsing 2147483648 overflows to -2147483648, but this
                           // value should be supported if there was a leading minus sign.
                           || localValue == Int32.MinValue;
                }*/

                // Same logic if positive, but one fewer characters is allowed (no minus sign)
                result = localValue;
                if (ptrLen < 10L) return true;
                if (ptrLen > 10L) return false;
                return *cString - '0' == localValue / 1000000000;
            }
        }
    }

    /// <summary>
    /// Jank intparse because the built-in one is pathetically slow. (because it needs to do actual work, you know)
    /// </summary>
    /// <param name="a">character to be parsed</param>
    /// <returns>hopefully an integer, no bloody warranty</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int homemadeUnsafeIntParse(char a) {
        return a - 48;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte homemadeUnsafeIntParse(byte a) {
        return (byte)(a - 48);
    }

    // we care even less.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int homemadeUnsafeIntParse(string a) {
        return a[0] - 48;
    }
}