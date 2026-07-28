/*
 * Happy Gopher Service
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using HappyGopher.Gopher;

namespace HappyGopher.Pages.Guestbook;

internal class SignGuestbookPage : IGopherPage
{
    public string Selector => throw new NotImplementedException();

    public Task<GopherResponseKind> WriteAsync(GopherRequest request, Stream output, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
