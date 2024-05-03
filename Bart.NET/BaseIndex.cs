// Copyright (c) 2024 Suzy Inc.
// SPDX-License-Identifier: MIT

namespace Bart.NET;

internal static class BaseIndex
{
    // hostMasks as lookup table
    private static readonly byte[] s_hostMasks =
    [
        0b1111_1111, // bits == 0
        0b0111_1111, // bits == 1
        0b0011_1111, // bits == 2
        0b0001_1111, // bits == 3
        0b0000_1111, // bits == 4
        0b0000_0111, // bits == 5
        0b0000_0011, // bits == 6
        0b0000_0001, // bits == 7
        0b0000_0000, // bits == 8
    ];

	// baseIndex of the first host route: prefixToBaseIndex(0,8)
	public const int FirstHostIndex = 0b1_0000_0000; // 256

	// baseIndex of the last host route: prefixToBaseIndex(255,8)
	public const int LastHostIndex = 0b1_1111_1111; // 511

    public const int StrideLength = 8; // one octet
    public const int MaxTreeDepth = 128 / StrideLength; // 16 (IPv6)
    public const int MaxNodeChildren = 1 << StrideLength; // 256 (possible route branches per octet)
    public const int MaxNodePrefixes = 1 << (StrideLength + 1); // 512

    /// <summary>
    /// Maps a prefix table as a complete binary tree. This is the
    /// so called <c>baseIndex</c> a.k.a. heapFunc.
    /// </summary>
    /// <param name="octet">An octet of the route prefix.</param>
    /// <param name="prefixLength">The length of the CIDR prefix covering the octet.</param>
    /// <returns>The base index to use within a bitset.</returns>
    public static uint PrefixToBaseIndex(byte octet, int prefixLength)
    {
        return (uint)(octet >> (StrideLength - prefixLength)) + (1U << prefixLength);
    }

    /// <summary>
    /// Maps an octet to a baseIndex, i.e. host routes. Used for
    /// host routes in a lookup.
    /// </summary>
    /// <param name="octet">An octet in the host prefix.</param>
    /// <returns>The base index to use within a bitset.</returns>
    public static uint OctetToBaseIndex(byte octet)
    {
        return octet + (uint)FirstHostIndex; // just: octet + 256
    }

    /// <summary>
    /// Finds the first octect for a network prefix in order
    /// to produce valid <see cref="IPNetwork"/> instances.
    /// </summary>
    /// <param name="octet">An unmasked octet.</param>
    /// <param name="bits">The bits of the CIDR prefix covering
    /// <paramref name="octet"/>.</param>
    /// <returns>An octet masked by the CIDR prefix, uncovering
    /// the first valid octet for the prefix.</returns>
    public static byte FirstOctetOfPrefix(byte octet, int bits)
    {
        return (byte)(octet & ~s_hostMasks[Math.Min(bits, 8)]);
    }

    /// <summary>
    /// Calculates the last host index of a prefix given an octet and the number of bits
    /// </summary>
    /// <param name="octet">An octet in the host prefix.</param>
    /// <param name="bits">The bits of the CIDR prefix covering
    /// <paramref name="octet"/>.</param>
    /// <returns>The last host index of the route prefix.</returns>
    public static uint LastHostIndexOfPrefix(byte octet, int bits)
    {
        return OctetToBaseIndex((byte)(octet | s_hostMasks[bits]));
    }

    /// <summary>
    /// Returns the lower and upper bounds of host routes for a given prefix
    /// </summary>
    /// <param name="index">An index of a prefix.</param>
    /// <returns>The lower and upper bounds of indexes for a prefix.</returns>
    public static (uint lowerBound, uint upperBound) LowerUpperBound(uint index)
    {
        (byte octet, int bits) = s_baseIndexLookup[index];
        return (OctetToBaseIndex(octet), LastHostIndexOfPrefix(octet, bits));
    }

    /// <summary>
    /// Calculates the prefix mask based on base index and depth
    /// </summary>
    /// <param name="baseIndex">The base index into a bitset.</param>
    /// <param name="depth">Depth of the index in the tree.</param>
    /// <returns>Prefix mask to use.</returns>
    public static int BaseIndexToPrefixMask(uint baseIndex, int depth)
    {
        (_, int prefixLength) = s_baseIndexLookup[baseIndex];
        return depth * StrideLength + prefixLength;
    }

    /// <summary>
    /// Calculates the host mask for the number of bits of CIDR prefix.
    /// </summary>
    /// <param name="bits">Bits to mask in a host octet.</param>
    /// <returns>The host mask.</returns>
    public static byte HostMask(int bits)
        => s_hostMasks[bits];

    /// <summary>
    /// Gets the octet and prefix length of a base index.
    /// </summary>
    /// <param name="baseIndex">A base index of a bitset.</param>
    /// <returns>A tuple containing the octet and prefix length.</returns>
    public static (byte octet, int prefixLength) BaseIndexToPrefix(uint baseIndex)
        => s_baseIndexLookup[baseIndex];

    // Inverse of prefixToBaseIndex, returns the octet and prefix length of a base index

    // octet and CIDR bits of baseIndex as lookup table.
    // Use the pre computed lookup table, bits.LeadingZeros is too slow.
    //
    //  func baseIndexToPrefix(baseIndex uint) (octet uint, pfxLen int) {
    //  	nlz := bits.LeadingZeros(baseIndex)
    //  	pfxLen = strconv.IntSize - nlz - 1
    //  	octet = (baseIndex & (0xFF >> (8 - pfxLen))) << (8 - pfxLen)
    //  	return octet, pfxLen
    //  }
    private static readonly (byte octet, int bits)[] s_baseIndexLookup =
    [
        new(0, -1),  // idx ==   0 invalid!
        new(0, 0),   // idx ==   1
        new(0, 1),   // idx ==   2
        new(128, 1), // idx ==   3
        new(0, 2),   // idx ==   4
        new(64, 2),  // idx ==   5
        new(128, 2), // idx ==   6
        new(192, 2), // idx ==   7
        new(0, 3),   // idx ==   8
        new(32, 3),  // idx ==   9
        new(64, 3),  // idx ==  10
        new(96, 3),  // idx ==  11
        new(128, 3), // idx ==  12
        new(160, 3), // idx ==  13
        new(192, 3), // idx ==  14
        new(224, 3), // idx ==  15
        new(0, 4),   // idx ==  16
        new(16, 4),  // idx ==  17
        new(32, 4),  // idx ==  18
        new(48, 4),  // idx ==  19
        new(64, 4),  // idx ==  20
        new(80, 4),  // idx ==  21
        new(96, 4),  // idx ==  22
        new(112, 4), // idx ==  23
        new(128, 4), // idx ==  24
        new(144, 4), // idx ==  25
        new(160, 4), // idx ==  26
        new(176, 4), // idx ==  27
        new(192, 4), // idx ==  28
        new(208, 4), // idx ==  29
        new(224, 4), // idx ==  30
        new(240, 4), // idx ==  31
        new(0, 5),   // idx ==  32
        new(8, 5),   // idx ==  33
        new(16, 5),  // idx ==  34
        new(24, 5),  // idx ==  35
        new(32, 5),  // idx ==  36
        new(40, 5),  // idx ==  37
        new(48, 5),  // idx ==  38
        new(56, 5),  // idx ==  39
        new(64, 5),  // idx ==  40
        new(72, 5),  // idx ==  41
        new(80, 5),  // idx ==  42
        new(88, 5),  // idx ==  43
        new(96, 5),  // idx ==  44
        new(104, 5), // idx ==  45
        new(112, 5), // idx ==  46
        new(120, 5), // idx ==  47
        new(128, 5), // idx ==  48
        new(136, 5), // idx ==  49
        new(144, 5), // idx ==  50
        new(152, 5), // idx ==  51
        new(160, 5), // idx ==  52
        new(168, 5), // idx ==  53
        new(176, 5), // idx ==  54
        new(184, 5), // idx ==  55
        new(192, 5), // idx ==  56
        new(200, 5), // idx ==  57
        new(208, 5), // idx ==  58
        new(216, 5), // idx ==  59
        new(224, 5), // idx ==  60
        new(232, 5), // idx ==  61
        new(240, 5), // idx ==  62
        new(248, 5), // idx ==  63
        new(0, 6),   // idx ==  64
        new(4, 6),   // idx ==  65
        new(8, 6),   // idx ==  66
        new(12, 6),  // idx ==  67
        new(16, 6),  // idx ==  68
        new(20, 6),  // idx ==  69
        new(24, 6),  // idx ==  70
        new(28, 6),  // idx ==  71
        new(32, 6),  // idx ==  72
        new(36, 6),  // idx ==  73
        new(40, 6),  // idx ==  74
        new(44, 6),  // idx ==  75
        new(48, 6),  // idx ==  76
        new(52, 6),  // idx ==  77
        new(56, 6),  // idx ==  78
        new(60, 6),  // idx ==  79
        new(64, 6),  // idx ==  80
        new(68, 6),  // idx ==  81
        new(72, 6),  // idx ==  82
        new(76, 6),  // idx ==  83
        new(80, 6),  // idx ==  84
        new(84, 6),  // idx ==  85
        new(88, 6),  // idx ==  86
        new(92, 6),  // idx ==  87
        new(96, 6),  // idx ==  88
        new(100, 6), // idx ==  89
        new(104, 6), // idx ==  90
        new(108, 6), // idx ==  91
        new(112, 6), // idx ==  92
        new(116, 6), // idx ==  93
        new(120, 6), // idx ==  94
        new(124, 6), // idx ==  95
        new(128, 6), // idx ==  96
        new(132, 6), // idx ==  97
        new(136, 6), // idx ==  98
        new(140, 6), // idx ==  99
        new(144, 6), // idx == 100
        new(148, 6), // idx == 101
        new(152, 6), // idx == 102
        new(156, 6), // idx == 103
        new(160, 6), // idx == 104
        new(164, 6), // idx == 105
        new(168, 6), // idx == 106
        new(172, 6), // idx == 107
        new(176, 6), // idx == 108
        new(180, 6), // idx == 109
        new(184, 6), // idx == 110
        new(188, 6), // idx == 111
        new(192, 6), // idx == 112
        new(196, 6), // idx == 113
        new(200, 6), // idx == 114
        new(204, 6), // idx == 115
        new(208, 6), // idx == 116
        new(212, 6), // idx == 117
        new(216, 6), // idx == 118
        new(220, 6), // idx == 119
        new(224, 6), // idx == 120
        new(228, 6), // idx == 121
        new(232, 6), // idx == 122
        new(236, 6), // idx == 123
        new(240, 6), // idx == 124
        new(244, 6), // idx == 125
        new(248, 6), // idx == 126
        new(252, 6), // idx == 127
        new(0, 7),   // idx == 128
        new(2, 7),   // idx == 129
        new(4, 7),   // idx == 130
        new(6, 7),   // idx == 131
        new(8, 7),   // idx == 132
        new(10, 7),  // idx == 133
        new(12, 7),  // idx == 134
        new(14, 7),  // idx == 135
        new(16, 7),  // idx == 136
        new(18, 7),  // idx == 137
        new(20, 7),  // idx == 138
        new(22, 7),  // idx == 139
        new(24, 7),  // idx == 140
        new(26, 7),  // idx == 141
        new(28, 7),  // idx == 142
        new(30, 7),  // idx == 143
        new(32, 7),  // idx == 144
        new(34, 7),  // idx == 145
        new(36, 7),  // idx == 146
        new(38, 7),  // idx == 147
        new(40, 7),  // idx == 148
        new(42, 7),  // idx == 149
        new(44, 7),  // idx == 150
        new(46, 7),  // idx == 151
        new(48, 7),  // idx == 152
        new(50, 7),  // idx == 153
        new(52, 7),  // idx == 154
        new(54, 7),  // idx == 155
        new(56, 7),  // idx == 156
        new(58, 7),  // idx == 157
        new(60, 7),  // idx == 158
        new(62, 7),  // idx == 159
        new(64, 7),  // idx == 160
        new(66, 7),  // idx == 161
        new(68, 7),  // idx == 162
        new(70, 7),  // idx == 163
        new(72, 7),  // idx == 164
        new(74, 7),  // idx == 165
        new(76, 7),  // idx == 166
        new(78, 7),  // idx == 167
        new(80, 7),  // idx == 168
        new(82, 7),  // idx == 169
        new(84, 7),  // idx == 170
        new(86, 7),  // idx == 171
        new(88, 7),  // idx == 172
        new(90, 7),  // idx == 173
        new(92, 7),  // idx == 174
        new(94, 7),  // idx == 175
        new(96, 7),  // idx == 176
        new(98, 7),  // idx == 177
        new(100, 7), // idx == 178
        new(102, 7), // idx == 179
        new(104, 7), // idx == 180
        new(106, 7), // idx == 181
        new(108, 7), // idx == 182
        new(110, 7), // idx == 183
        new(112, 7), // idx == 184
        new(114, 7), // idx == 185
        new(116, 7), // idx == 186
        new(118, 7), // idx == 187
        new(120, 7), // idx == 188
        new(122, 7), // idx == 189
        new(124, 7), // idx == 190
        new(126, 7), // idx == 191
        new(128, 7), // idx == 192
        new(130, 7), // idx == 193
        new(132, 7), // idx == 194
        new(134, 7), // idx == 195
        new(136, 7), // idx == 196
        new(138, 7), // idx == 197
        new(140, 7), // idx == 198
        new(142, 7), // idx == 199
        new(144, 7), // idx == 200
        new(146, 7), // idx == 201
        new(148, 7), // idx == 202
        new(150, 7), // idx == 203
        new(152, 7), // idx == 204
        new(154, 7), // idx == 205
        new(156, 7), // idx == 206
        new(158, 7), // idx == 207
        new(160, 7), // idx == 208
        new(162, 7), // idx == 209
        new(164, 7), // idx == 210
        new(166, 7), // idx == 211
        new(168, 7), // idx == 212
        new(170, 7), // idx == 213
        new(172, 7), // idx == 214
        new(174, 7), // idx == 215
        new(176, 7), // idx == 216
        new(178, 7), // idx == 217
        new(180, 7), // idx == 218
        new(182, 7), // idx == 219
        new(184, 7), // idx == 220
        new(186, 7), // idx == 221
        new(188, 7), // idx == 222
        new(190, 7), // idx == 223
        new(192, 7), // idx == 224
        new(194, 7), // idx == 225
        new(196, 7), // idx == 226
        new(198, 7), // idx == 227
        new(200, 7), // idx == 228
        new(202, 7), // idx == 229
        new(204, 7), // idx == 230
        new(206, 7), // idx == 231
        new(208, 7), // idx == 232
        new(210, 7), // idx == 233
        new(212, 7), // idx == 234
        new(214, 7), // idx == 235
        new(216, 7), // idx == 236
        new(218, 7), // idx == 237
        new(220, 7), // idx == 238
        new(222, 7), // idx == 239
        new(224, 7), // idx == 240
        new(226, 7), // idx == 241
        new(228, 7), // idx == 242
        new(230, 7), // idx == 243
        new(232, 7), // idx == 244
        new(234, 7), // idx == 245
        new(236, 7), // idx == 246
        new(238, 7), // idx == 247
        new(240, 7), // idx == 248
        new(242, 7), // idx == 249
        new(244, 7), // idx == 250
        new(246, 7), // idx == 251
        new(248, 7), // idx == 252
        new(250, 7), // idx == 253
        new(252, 7), // idx == 254
        new(254, 7), // idx == 255
        new(0, 8),   // idx == 256 firstHostIndex
        new(1, 8),   // idx == 257
        new(2, 8),   // idx == 258
        new(3, 8),   // idx == 259
        new(4, 8),   // idx == 260
        new(5, 8),   // idx == 261
        new(6, 8),   // idx == 262
        new(7, 8),   // idx == 263
        new(8, 8),   // idx == 264
        new(9, 8),   // idx == 265
        new(10, 8),  // idx == 266
        new(11, 8),  // idx == 267
        new(12, 8),  // idx == 268
        new(13, 8),  // idx == 269
        new(14, 8),  // idx == 270
        new(15, 8),  // idx == 271
        new(16, 8),  // idx == 272
        new(17, 8),  // idx == 273
        new(18, 8),  // idx == 274
        new(19, 8),  // idx == 275
        new(20, 8),  // idx == 276
        new(21, 8),  // idx == 277
        new(22, 8),  // idx == 278
        new(23, 8),  // idx == 279
        new(24, 8),  // idx == 280
        new(25, 8),  // idx == 281
        new(26, 8),  // idx == 282
        new(27, 8),  // idx == 283
        new(28, 8),  // idx == 284
        new(29, 8),  // idx == 285
        new(30, 8),  // idx == 286
        new(31, 8),  // idx == 287
        new(32, 8),  // idx == 288
        new(33, 8),  // idx == 289
        new(34, 8),  // idx == 290
        new(35, 8),  // idx == 291
        new(36, 8),  // idx == 292
        new(37, 8),  // idx == 293
        new(38, 8),  // idx == 294
        new(39, 8),  // idx == 295
        new(40, 8),  // idx == 296
        new(41, 8),  // idx == 297
        new(42, 8),  // idx == 298
        new(43, 8),  // idx == 299
        new(44, 8),  // idx == 300
        new(45, 8),  // idx == 301
        new(46, 8),  // idx == 302
        new(47, 8),  // idx == 303
        new(48, 8),  // idx == 304
        new(49, 8),  // idx == 305
        new(50, 8),  // idx == 306
        new(51, 8),  // idx == 307
        new(52, 8),  // idx == 308
        new(53, 8),  // idx == 309
        new(54, 8),  // idx == 310
        new(55, 8),  // idx == 311
        new(56, 8),  // idx == 312
        new(57, 8),  // idx == 313
        new(58, 8),  // idx == 314
        new(59, 8),  // idx == 315
        new(60, 8),  // idx == 316
        new(61, 8),  // idx == 317
        new(62, 8),  // idx == 318
        new(63, 8),  // idx == 319
        new(64, 8),  // idx == 320
        new(65, 8),  // idx == 321
        new(66, 8),  // idx == 322
        new(67, 8),  // idx == 323
        new(68, 8),  // idx == 324
        new(69, 8),  // idx == 325
        new(70, 8),  // idx == 326
        new(71, 8),  // idx == 327
        new(72, 8),  // idx == 328
        new(73, 8),  // idx == 329
        new(74, 8),  // idx == 330
        new(75, 8),  // idx == 331
        new(76, 8),  // idx == 332
        new(77, 8),  // idx == 333
        new(78, 8),  // idx == 334
        new(79, 8),  // idx == 335
        new(80, 8),  // idx == 336
        new(81, 8),  // idx == 337
        new(82, 8),  // idx == 338
        new(83, 8),  // idx == 339
        new(84, 8),  // idx == 340
        new(85, 8),  // idx == 341
        new(86, 8),  // idx == 342
        new(87, 8),  // idx == 343
        new(88, 8),  // idx == 344
        new(89, 8),  // idx == 345
        new(90, 8),  // idx == 346
        new(91, 8),  // idx == 347
        new(92, 8),  // idx == 348
        new(93, 8),  // idx == 349
        new(94, 8),  // idx == 350
        new(95, 8),  // idx == 351
        new(96, 8),  // idx == 352
        new(97, 8),  // idx == 353
        new(98, 8),  // idx == 354
        new(99, 8),  // idx == 355
        new(100, 8), // idx == 356
        new(101, 8), // idx == 357
        new(102, 8), // idx == 358
        new(103, 8), // idx == 359
        new(104, 8), // idx == 360
        new(105, 8), // idx == 361
        new(106, 8), // idx == 362
        new(107, 8), // idx == 363
        new(108, 8), // idx == 364
        new(109, 8), // idx == 365
        new(110, 8), // idx == 366
        new(111, 8), // idx == 367
        new(112, 8), // idx == 368
        new(113, 8), // idx == 369
        new(114, 8), // idx == 370
        new(115, 8), // idx == 371
        new(116, 8), // idx == 372
        new(117, 8), // idx == 373
        new(118, 8), // idx == 374
        new(119, 8), // idx == 375
        new(120, 8), // idx == 376
        new(121, 8), // idx == 377
        new(122, 8), // idx == 378
        new(123, 8), // idx == 379
        new(124, 8), // idx == 380
        new(125, 8), // idx == 381
        new(126, 8), // idx == 382
        new(127, 8), // idx == 383
        new(128, 8), // idx == 384
        new(129, 8), // idx == 385
        new(130, 8), // idx == 386
        new(131, 8), // idx == 387
        new(132, 8), // idx == 388
        new(133, 8), // idx == 389
        new(134, 8), // idx == 390
        new(135, 8), // idx == 391
        new(136, 8), // idx == 392
        new(137, 8), // idx == 393
        new(138, 8), // idx == 394
        new(139, 8), // idx == 395
        new(140, 8), // idx == 396
        new(141, 8), // idx == 397
        new(142, 8), // idx == 398
        new(143, 8), // idx == 399
        new(144, 8), // idx == 400
        new(145, 8), // idx == 401
        new(146, 8), // idx == 402
        new(147, 8), // idx == 403
        new(148, 8), // idx == 404
        new(149, 8), // idx == 405
        new(150, 8), // idx == 406
        new(151, 8), // idx == 407
        new(152, 8), // idx == 408
        new(153, 8), // idx == 409
        new(154, 8), // idx == 410
        new(155, 8), // idx == 411
        new(156, 8), // idx == 412
        new(157, 8), // idx == 413
        new(158, 8), // idx == 414
        new(159, 8), // idx == 415
        new(160, 8), // idx == 416
        new(161, 8), // idx == 417
        new(162, 8), // idx == 418
        new(163, 8), // idx == 419
        new(164, 8), // idx == 420
        new(165, 8), // idx == 421
        new(166, 8), // idx == 422
        new(167, 8), // idx == 423
        new(168, 8), // idx == 424
        new(169, 8), // idx == 425
        new(170, 8), // idx == 426
        new(171, 8), // idx == 427
        new(172, 8), // idx == 428
        new(173, 8), // idx == 429
        new(174, 8), // idx == 430
        new(175, 8), // idx == 431
        new(176, 8), // idx == 432
        new(177, 8), // idx == 433
        new(178, 8), // idx == 434
        new(179, 8), // idx == 435
        new(180, 8), // idx == 436
        new(181, 8), // idx == 437
        new(182, 8), // idx == 438
        new(183, 8), // idx == 439
        new(184, 8), // idx == 440
        new(185, 8), // idx == 441
        new(186, 8), // idx == 442
        new(187, 8), // idx == 443
        new(188, 8), // idx == 444
        new(189, 8), // idx == 445
        new(190, 8), // idx == 446
        new(191, 8), // idx == 447
        new(192, 8), // idx == 448
        new(193, 8), // idx == 449
        new(194, 8), // idx == 450
        new(195, 8), // idx == 451
        new(196, 8), // idx == 452
        new(197, 8), // idx == 453
        new(198, 8), // idx == 454
        new(199, 8), // idx == 455
        new(200, 8), // idx == 456
        new(201, 8), // idx == 457
        new(202, 8), // idx == 458
        new(203, 8), // idx == 459
        new(204, 8), // idx == 460
        new(205, 8), // idx == 461
        new(206, 8), // idx == 462
        new(207, 8), // idx == 463
        new(208, 8), // idx == 464
        new(209, 8), // idx == 465
        new(210, 8), // idx == 466
        new(211, 8), // idx == 467
        new(212, 8), // idx == 468
        new(213, 8), // idx == 469
        new(214, 8), // idx == 470
        new(215, 8), // idx == 471
        new(216, 8), // idx == 472
        new(217, 8), // idx == 473
        new(218, 8), // idx == 474
        new(219, 8), // idx == 475
        new(220, 8), // idx == 476
        new(221, 8), // idx == 477
        new(222, 8), // idx == 478
        new(223, 8), // idx == 479
        new(224, 8), // idx == 480
        new(225, 8), // idx == 481
        new(226, 8), // idx == 482
        new(227, 8), // idx == 483
        new(228, 8), // idx == 484
        new(229, 8), // idx == 485
        new(230, 8), // idx == 486
        new(231, 8), // idx == 487
        new(232, 8), // idx == 488
        new(233, 8), // idx == 489
        new(234, 8), // idx == 490
        new(235, 8), // idx == 491
        new(236, 8), // idx == 492
        new(237, 8), // idx == 493
        new(238, 8), // idx == 494
        new(239, 8), // idx == 495
        new(240, 8), // idx == 496
        new(241, 8), // idx == 497
        new(242, 8), // idx == 498
        new(243, 8), // idx == 499
        new(244, 8), // idx == 500
        new(245, 8), // idx == 501
        new(246, 8), // idx == 502
        new(247, 8), // idx == 503
        new(248, 8), // idx == 504
        new(249, 8), // idx == 505
        new(250, 8), // idx == 506
        new(251, 8), // idx == 507
        new(252, 8), // idx == 508
        new(253, 8), // idx == 509
        new(254, 8), // idx == 510
        new(255, 8), // idx == 511
    ];
}
