# `SocketError.OperationAborted` after a page load

A browser window that has finished loading a page throws, roughly a minute and a
half later, with no user action in between:

```
System.IO.IOException
  Message=Unable to read data from the transport connection: Der E/A-Vorgang wurde wegen
          eines Threadendes oder einer Anwendungsanforderung abgebrochen..
  Source=System.Net.Sockets
  StackTrace:
   at System.Net.Sockets.Socket.AwaitableSocketAsyncEventArgs.ThrowException(SocketError error,
      CancellationToken cancellationToken)

Inner Exception 1:
System.Net.Sockets.SocketException: 'Der E/A-Vorgang wurde wegen eines Threadendes oder
einer Anwendungsanforderung abgebrochen.'
```

The German text is how Windows spells error 995, `ERROR_OPERATION_ABORTED`
("the I/O operation has been aborted because of either a thread exit or an
application request") — the OS message behind `SocketError.OperationAborted`.
The same failure on Linux reads `Operation canceled`.

**It is not a Broiler thread exiting, and no I/O of Broiler's was lost.** It is
`SocketsHttpHandler` closing one of its own pooled connections, and it is
handled inside the HTTP stack: the page is already rendered, the browser keeps
working, nothing is cancelled. It becomes visible only under a debugger
configured to break on thrown exceptions, or through
`AppDomain.CurrentDomain.FirstChanceException`.

## Mechanism

An idle keep-alive connection in `SocketsHttpHandler`'s pool cannot tell a
server-side close from a still-healthy connection without looking, so the pool's
scavenger arms each idle connection with a **pending zero-byte read**. When the
pool then closes that connection — because it passed `PooledConnectionIdleTimeout`
(default one minute), because its `PooledConnectionLifetime` expired, or because
the `HttpClient` was disposed — the socket goes away underneath that pending
read, and the read fails with `OperationAborted`. Since the read's own
cancellation token was never cancelled, `AwaitableSocketAsyncEventArgs.ThrowException`
reports the socket error rather than an `OperationCanceledException`, and
`NetworkStream` wraps it as `IOException`. That is the whole stack, which is why
the trace contains no Broiler frame.

Measured against a loopback keep-alive server on .NET 10 (`net10.0`, Linux; the
Windows message differs, the `SocketError` does not):

| Scenario | Result |
| --- | --- |
| Request, then stay idle with the client alive | one `IOException`/`OperationAborted` **75 s** after the request — the idle-timeout eviction |
| Request, idle 45 s, then dispose the `HttpClient` | one `IOException`/`OperationAborted` at the moment of disposal |
| Request, dispose the `HttpClient` immediately | none — no read-ahead had been armed yet |

Both of the first two applied to the browser, which is why the exception landed
about 80 seconds after a `https://www.google.com` navigation.

`Broiler.Cli` does not show it for a reason that is not to its credit: a capture
process exits within seconds of the fetch, long before its pooled connections
reach the idle timeout, and it is not usually run under a debugger.

## What Broiler was doing wrong

`BrowserApp.LoadUrlOnWorkerAsync` built `new PageLoader(new HttpClient())` per
navigation inside a `using` scope, and `PageLoader.Dispose` disposed that client
— so every navigation stood up a private connection pool and tore it down again
at the end of the load, hitting the disposal row of the table above every time,
on top of reconnecting to hosts the previous page was already connected to.

The browser now keeps **one** `HttpClient` for the process
(`BrowserApp.PageHttpClient`) and `PageLoader` no longer disposes a client it was
merely handed; ownership is explicit via its `ownsHttpClient` parameter, and
`PageLoaderLifetimeTests` covers both halves. Connections are now reused across
navigations, so continued browsing keeps them warm instead of leaving them to be
evicted.

## What remains

The idle-eviction row cannot be fixed from here — it is internal to
`SocketsHttpHandler`, and any pool that closes an idle connection can emit it. A
window left open on a loaded page will still produce one first-chance
`IOException` per pooled connection about a minute after the page goes quiet.
It is safe to continue past; in Visual Studio, untick
**Debug → Windows → Exception Settings → Common Language Runtime Exceptions →
System.IO.IOException** to stop breaking on it.

Note that the *frequency* is a fair signal of client hygiene: one of these per
navigation means something is still creating and disposing an `HttpClient` per
request. `HtmlContainerInt.TryLoadRemoteFont` in `Broiler.HTML` still does this
per web font (a `using var client = new HttpClient(...)` around one
`GetByteArrayAsync`); it disposes immediately, so it matches the harmless third
row rather than the second, but it is the same anti-pattern and the same socket
churn.
