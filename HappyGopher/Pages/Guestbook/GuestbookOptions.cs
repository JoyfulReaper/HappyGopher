/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace HappyGopher.Pages.Guestbook;

public class GuestbookOptions
{
    public const string SectionName = "Guestbook";

    public bool Enabled { get; set; } = false;
    public string DataPath { get; set; } = "data/guestbook.jsonl";
    public int MaxEntriesDisplayed { get; set; } = 50;
}
