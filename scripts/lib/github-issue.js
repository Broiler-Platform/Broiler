'use strict';

/**
 * File a suite's finding as a GitHub issue, refreshing the one already open for
 * it instead of opening a second.
 *
 * The WPT workflows open a fresh issue per run, which suits a suite a human
 * dispatches and then reads.  The privacy test pages run weekly, unattended,
 * over a corpus whose gaps change slowly: filing the same backlog every Tuesday
 * would bury the copy somebody is already working from under fifty near-identical
 * copies.  So each issue body carries a hidden marker naming its kind, and a run
 * updates the open issue carrying that marker — same issue, same subscribers,
 * current numbers — and only opens a new one when there is none.
 *
 * The marker is matched by reading the open issues rather than through the search
 * API: an HTML comment is not reliably searchable, and search results lag behind
 * writes by enough that a weekly job would sometimes open the duplicate this
 * exists to avoid.
 */

// One page is 100 issues; ten pages is far past any backlog this suite's issues
// would be filed into, and bounds the cost of a repository with a long history
// of open issues.
const MAX_PAGES = 10;
const PER_PAGE = 100;

/**
 * Find the open issue carrying `marker` in its body, oldest first so that the
 * original survives if a duplicate was ever opened by hand.
 */
async function findOpenIssueByMarker({ github, owner, repo, marker }) {
    if (!marker)
        throw new Error('a marker is required to find the issue to refresh');

    for (let page = 1; page <= MAX_PAGES; page++) {
        const response = await github.rest.issues.listForRepo({
            owner,
            repo,
            state: 'open',
            sort: 'created',
            direction: 'asc',
            per_page: PER_PAGE,
            page,
        });
        const issues = response.data || [];
        // listForRepo returns pull requests too; they carry a `pull_request` key.
        const match = issues.find(issue => !issue.pull_request && (issue.body || '').includes(marker));
        if (match)
            return match;
        if (issues.length < PER_PAGE)
            return null;
    }
    return null;
}

/** The comment a refresh leaves, so subscribers see the run behind the edit. */
function buildRefreshComment({ title, runUrl }) {
    const run = runUrl ? `[the latest run](${runUrl})` : 'the latest run';
    return `Refreshed from ${run}: ${title}`;
}

/**
 * Create the issue, or update the open one carrying the same marker.
 *
 * Returns `{action, number, url}` with `action` either `created` or `updated`.
 */
async function createOrUpdateIssue({ github, owner, repo, marker, title, body, runUrl }) {
    if (!title)
        throw new Error('an issue title is required');
    // Defensive: the body generator writes the marker, and an issue whose body
    // lost it would be duplicated by every later run.
    const fullBody = body.includes(marker) ? body : `${marker}\n\n${body}`;

    const existing = await findOpenIssueByMarker({ github, owner, repo, marker });
    if (!existing) {
        const created = await github.rest.issues.create({ owner, repo, title, body: fullBody });
        return { action: 'created', number: created.data.number, url: created.data.html_url };
    }

    await github.rest.issues.update({
        owner,
        repo,
        issue_number: existing.number,
        title,
        body: fullBody,
    });
    await github.rest.issues.createComment({
        owner,
        repo,
        issue_number: existing.number,
        body: buildRefreshComment({ title, runUrl }),
    });
    return { action: 'updated', number: existing.number, url: existing.html_url };
}

module.exports = { findOpenIssueByMarker, buildRefreshComment, createOrUpdateIssue, MAX_PAGES, PER_PAGE };
