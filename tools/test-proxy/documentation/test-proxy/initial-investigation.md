# Test Proxy Server

It is common practice to test service client code by recording HTTP requests and
responses during a test run against a live endpoint, and then playing back the
matching responses to requests in subsequent runs. This testing technique is
used already in our many of our SDKs, and is on the backlog for the others.

Currently, the SDKs that do have this handle the recording and playback
in-process by effectively mocking out the portion of code that would otherwise
have hit the live endpoint. These systems are thereforere language-specific and
cannot be shared between SDKs.

This document looks at how we might instead use an out-of-process,
cross-language server to handle the playback and recording. The basic idea of is
to have a test server that sits between the client being tested and the live
endpoint. Instead of mocking out the communication with the server, the
communication can be redirected to a test server. We also believe that much of
this same technology could be leveraged for SDK perf and stress testing.

A lot of the notes here are based on a kickoff meeting that was recorded:
https://msit.microsoftstream.com/video/54f1a3ff-0400-9fb2-b497-f1eb0a599ca1

A small prototype/proof-of-concept was built with a C# based ASP.NET server and
it was demonstrated that we can run JavaScript and C# tests against it. This
document also discusses the prototype, where to find it, and how to run it.

This project is now being handed off and this document aims to capture what has
been discussed and tried so far as part of that.

# Goals

## Share code

Today, each SDK has its own testing solution. Some leverage third party
components while others are written from scratch. Where applicable, the third
party components still need work to integrate them into our infrastructure. So
either way, each new SDK incurs initial development costs and increases ongoing
maintenance costs.

By moving the playback and recording out-of-process to a shared system, we can
reuse more code across SDKs. However, it should be noted that the costs would
not drop to zero per SDK. There still needs to be "glue" code that is
language-specific that wires up the tests to the test server. In the prototype,
the recording and playback portion of the existing C# test framework is split
off into a separate ASP.NET process while another portion is changed to redirect
requests to the test server rather than mocking out the HTTP call. The latter
portion would still have to be implemented in each language.

In addition to reusing the general code for recording and playback, there is
also service-specific code needed to handle correct sanitization of secrets and
filtering. In the out-of-process approach, the same code could be written and
maintained once per service rather than once per service, per language. There is
more than the cost savings of sharing that logic to consider. That said, the
prototype does not handle per-service sanitization at all yet, and the test server is
hard-wired to sanitization defaults.


## Make tests exercise a more realistic code path

Since the client would communicate with the test server over genuine HTTP, the code
under test in the client would follow a code path much closer to live tests during 
playback. 

Past experience has shown that this additional coverage would be valuable. For example,
Python SDK tests once failed to find issues to due problems with the mocking of async
vs sync calls.

If we do use this same technology for performance testing (see below), this
aspect is doubly important. We cannot do meaningful performance testing without
actually contacting a remote server as the client would in a real scenario.

We also discussed that this new system could allow for fault injection by having
the test server error out.


## Performance testing

The idea for performance testing is to eliminate any server-side bottleneck from
a benchmark so that the limits of the client can be tested. For example, a real
service might throttle clients that make too many calls in a given timeframe. In
that case, a live performance test would fail to uncover client-side perf issues
that may be lurking should that throttling be reduced.

The solution to that problem can be very similar to the recording/playback
solution of functional tests. Instead of contacting the live service, a test
service can respond with recorded/cached responses. The main distinction is
that for performance case the test server must respond basically as fast as
possible while the functional tests do not have that constraint. Nevertheless,
we believe that if we take the general approach of the prototype further, we can
meet this goal. It is currently just a small amount of ASP.NET that is heavily
inspired by an earlier performance testing prototype.

We also discussed that if the recording/playback solution isn't fast enough to
handle performance testing directly, we could possibly solve that by having a
fast caching layer that sits between it and the client for performance tests. We
don't think this would be necessary if we take appropriate care in the test
server implementation.


## Share recording format

These test frameworks all serialize requests and their matching responses to
disk, but each SDK has its own format. There are various issues that arise with
these files being large and churning too much in the repo. By consolidating on
one solution with one format, we can give ourself a path to address these pain
points for all languages at once. 

There is a desire to get the recordings out of the repo altogether. This is a
somewhat orthogonal goal to in-proc/out-of-proc and this has been investigated
before and there are some hard problems there. We think we can keep this out of
scope for the initial solution. The prototype runs the test server locally and
stores files on local disk just like the in-proc testing does. Nevertheless,
consolidating on a single solution and recording format could make it easier in
the future to have the storage for recordings moved elsewhere than the repo. For
expedience, the prototype passes file paths back and forth between client and
server. This should probably be changed so that the protocol with the server is
more abstract and the client needn't know where the files are physcially located
or even if they are on the local disk, etc.

The question of sharing the actual specific recordings between languages was
also raised, but we have decided this is a non-goal. Recordings are heavily
coupled to tests, and typically there is one recording file per test. Since the
tests requests state changes on the service side, the responses are then
sensitive to requests that came earlier in the test. In order to share
recordings, the tests in different languages would have to be exactly the same.
(It is maybe conceivable to imagine some day that we generate tests for
different languages from some sort of shared test description, but that's
definitely getting way out of scope here.)

There is still some value in having the same format and being able to read and
compare recordings in other languages to see how their clients might differ from
one being developed for another language.


## Retain offline testing experience

We do not want to introduce a requirement for a new service to be up and
accessible for these tests to pass. This could add more flakiness and create a
new critical piece of live infrastructure that needs to be up for devs to be
productive. 

As mentioned above the prototype runs the test server locally and serves files
from the local disk so this goal is trivially met there.

That said, there could be advantages to remotely hosting the test server. It
could be one way to move the recordings out of the repo. It could also allow
us to have more test run telemetry. If we do go down this road at some point,
we should still offer an offline alternative.


## Minimal impact on development experience

There is some risk with introducing this infrastructure that it might add complexity
to dev workflows or otherwise slow them down.

We need to avoid adding machine dependencies that complicate dev setup. We have
discussed running the test server in a container such that Docker is all that is
required and we remain free to pick any implementation language for the server.
That said, SDK development doesn't currently require Docker and even that could
be an extra moving part that adds some friction. Still, adding a docker
dependency could open other infrastructure doors in the future and this might be
a good forcing function to start requiring it for SDK development.

There's some prior art here with the autorest test server, but it requires node
and is not currently containerized.

JavaScript also has some prior art with out-of-proc testing for browser tests.

In addition to not taking unnecessary extra dependencies, running the tests with
the new process should be nearly as fast as the current approaches and nearly as
easy to do.


# Non-Goals

## Non-HTTP protocols

At least initially, we do not plan to support protocols other than HTTP.


# Alternatives

It is worth thinking about alternatives to the out-of-proc solution to make sure that
we are getting good value for its costs:

Some that have been mentioned:
* In-proc HTTP server: allows for more real code coverage, but still language specific.
* Sharing recording format as a standard without sharing the implementation.
* Sharing sanitization rules by describing them in some common format rather than code.

There is also the possibility that we use the out-of-proc approach but rely on
an existing third party solution for that rather than building production
infrastructure along the lines in the prototype. 

The most promising external tech in this space seems to be WireMock:
(http://wiremock.org). There is a hosted, commerical offering called MockLab
(http://get.mocklab.io/), but we can also run OSS WireMock ourselves in a
container or wherever else. Laurent did some experiments with WireMock a while
back and had a good experience. There was some concern in the meeting that the
performance may not be adequate for our performance testing needs, but this has
not been checked yet. It would probably be a good idea to take a deeper look at
WireMock and compare and contrast it to developing something more real based on
the prototype.


# Roadmap / Roll-out strategy

We discussed splitting the roll-out for this technology between new SDKs that
have no record/playback tests yet and converting existing SDKs over. It will be
harder to get to feature and experience parity with the exsting
language-specific frameworks. As such, we will get more benefit sooner and for
less cost if we start with the SDKs that don't have a solution yet.

In our meeting, we discussed C and Go SDKs as potentially good targets for early
adoption, but in talking to the respective teams, these may not be most
appropriate. The C SDK uses MQTTP not HTTP for IOT and Go actually has a solution
already. There is now more interest in doing C++ and mobile SDKs first.

We also briefly discussed the idea of making this testing solution into
something we can package and share with customers so that they can test their
own scenarios. There's some thinking that this could be aligned with our OKRs.

Potentially, the roadmap could be ordered like this:

1. Recording/playback for new languages
2. Performance testing
3. Recording/playback migration for existing languages
4. Sharing tech with customers

# Implementation

The prototype that used to be here is now obsolete. The original prototype was used as the base of the tool available under `eng/tools/test-proxy/Azure.Sdk.Tools.TestProxy/`. It is documented [here](Azure.Sdk.Tools.TestProxy/README.md). Try it out!

If you're still curious, the original prototypes are located in Nick Guerrera's forks of azure-sdk-for-js and net.

- [nguerrera/azure-sdk-for-js](https://github.com/nguerrera/azure-sdk-for-js/tree/oop-hack)
- [nguerrera/azure-sdk-for-net](https://github.com/nguerrera/azure-sdk-for-net/tree/oop-hack)
