// ---
// skill: the-standard-brokers
// type: template
// source-section: "1. Brokers"
// ---

// SecurityContextPrincipalFactory.cs — shared claims-building helper
using System.Collections.Generic;
using System.Security.Claims;
using {Namespace}.Models.Events;

namespace {Namespace}.Brokers.Securities
{
    /// <summary>
    /// Rebuilds a <see cref="ClaimsPrincipal"/> from an event envelope's normalized actor, so the
    /// security client's pipeline — which resolves a user id from <c>oid</c> / <c>nameidentifier</c>
    /// claims — sees the ORIGINAL caller regardless of what identity the current process runs under.
    ///
    /// <para><b>There is exactly one of these, and that is the point.</b> Every broker that needs
    /// to turn a <see cref="SecurityContext"/> into a <see cref="ClaimsPrincipal"/> —
    /// <c>SecurityAuditBroker</c> (see <c>Template_SecurityAuditBroker.cs</c>), an access broker
    /// that compares an actor against an entity's CreatedBy, or any later addition with the same
    /// need — must call this rather than building its own copy. A second copy would not fail
    /// loudly; it would quietly build a slightly different principal and let two call sites
    /// silently disagree about who the actor is.</para>
    /// </summary>
    internal static class SecurityContextPrincipalFactory
    {
        public static ClaimsPrincipal Create(SecurityContext securityContext)
        {
            var claims = new List<Claim>();

            if (string.IsNullOrWhiteSpace(securityContext?.SubjectId) is false)
                claims.Add(new Claim(ClaimTypes.NameIdentifier, securityContext!.SubjectId!));

            if (string.IsNullOrWhiteSpace(securityContext?.Username) is false)
                claims.Add(new Claim(ClaimTypes.Name, securityContext!.Username!));

            foreach (string role in securityContext?.Roles ?? [])
                claims.Add(new Claim(ClaimTypes.Role, role));

            ClaimsIdentity identity = securityContext?.IsAuthenticated == true
                ? new ClaimsIdentity(claims, authenticationType: "EventEnvelope")
                : new ClaimsIdentity(claims);

            return new ClaimsPrincipal(identity);
        }
    }
}
