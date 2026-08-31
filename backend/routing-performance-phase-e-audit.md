# Phase E Valhalla call audit

This audit describes the routing calls as they exist before Phase E caching.
The classification is about endpoint provenance for one immutable
`RoutingNetworkSnapshot` version:

- **A — fully static:** every coordinate is defined by the snapshot.
- **B — partially dynamic:** at least one endpoint is snapshot-defined and at
  least one is derived from the passenger request or a request-specific route
  projection.
- **C — fully dynamic:** the endpoints are passenger/request-defined.

All cache keys introduced by Phase E retain exact IEEE-754 coordinate values;
the classification does not permit coordinate rounding.

| Routing stage | Call site | Purpose | Classification | Phase E treatment |
| --- | --- | --- | --- | --- |
| Access discovery | `RoutingService.DiscoverBoardAccessOptionsAsync` -> `GetMatrixAsync` | Passenger origin to the route's candidate boarding anchors | B: live origin; snapshot samples/search anchors plus request-projected anchors | Exact, versioned matrix cache. No static precomputation. |
| Transfer generation | `RoutingService.FindTransferCandidates` | Builds provisional candidates from the snapshot interchange graph | No Valhalla call in this stage | No cache operation. Authoritative paths remain deferred to confirmation. |
| Confirmation | `ConfirmJourneyCandidatesAsync` transfer tasks -> `GetMatrixAsync` | Walking between two route interchange sample points | A: both points are copied from `RoutingNetworkSnapshot.RouteSamples` | Exact, versioned, lazily populated static-transfer cache. This retains Valhalla as authority and avoids an eager snapshot-build latency spike. |
| Confirmation | `ConfirmWalkingAccessAsync` | Passenger origin to boarding anchor, or alighting anchor to passenger destination | B | Exact, versioned matrix cache. |
| Confirmation | `ConfirmSingleAccessAsync` pedestrian portion | Passenger/route anchor to TODA | B in general: TODA is static, but the route anchor can be a request-specific projection | Exact, versioned matrix cache. It is deliberately not counted as a proven static TODA pair. |
| Confirmation | `ConfirmSingleAccessAsync` tricycle portion | TODA to route anchor or passenger destination | B in general for the same projection reason; TODA to passenger is also B | Exact, versioned matrix cache. No blanket TODA-anchor precomputation. |
| Confirmation | `ConfirmOriginAccessPathCompletionsAsync.GetBoardRoute` | TODA-origin tricycle road path to a candidate board anchor | B: the board anchor may be request-projected | Exact, versioned route cache. |
| Confirmation | `ConfirmOriginAccessPathCompletionsAsync.GetDirectRoute` | TODA-origin tricycle road path to the passenger destination | B | Exact, versioned route cache. |
| Geometry enrichment | `GetRoadGeometryAsync` | Road geometry for selected walk/tricycle legs | B or C depending on the selected leg | Exact, versioned route cache. Jeepney geometry is already sliced from the snapshot and does not call Valhalla. |
| Nearby-route API | `FindNearbyRoutesAsync` -> `GetMatrixAsync` | Live location to static route samples | B | Exact, versioned matrix cache when executed by `RoutingService`. |
| Legacy direct connection confirmation | `ConfirmConnectionsAsync` -> `ConfirmAccessAsync` | Origin/destination access confirmation | B | Exact, versioned matrix cache through the same wrapper. |

## Static transfer decision

`WalkSegmentCandidate` instances created by `RoutingService.TransferGraph` copy
both endpoints from the versioned `_routeSamples` dictionaries. Their
authoritative pedestrian matrix result is therefore safe to reuse only with
the same snapshot version, costing profile, exact ordered endpoints, and
Valhalla request options. Phase E caches these lazily instead of precomputing
all interchange pairs: eager precomputation would move a potentially large
number of Valhalla calls into the first snapshot build and is unnecessary for
pairs no planned journey uses.

## TODA-to-anchor decision

TODA centers are snapshot-defined, but the route-side access coordinate is not
always a snapshot sample. Exact and projected boarding/alighting anchors can
depend on the passenger coordinates and route occurrence selected for that
request. Treating every TODA-to-anchor request as static would therefore be an
incorrect provenance assumption. Phase E does not precompute that category.
Repeated exact pairs still benefit from the general versioned cache without
changing their coordinates or replacing Valhalla distance/time.

## Failure and invalidation policy

Only successfully completed, parsed Valhalla responses are inserted. Timeouts,
HTTP failures, malformed responses, and canceled underlying operations are
removed from the single-flight table and are immediately retryable. A caller's
cancellation cancels only that caller's wait; it cannot cancel shared work for
other coalesced callers. Cache entries have bounded weighted storage plus
sliding and absolute expiration. Snapshot-dependent keys include the pinned
snapshot version, so an invalidation and atomic snapshot swap makes all older
static results unreachable to new requests without disrupting requests already
pinned to the old immutable snapshot.
