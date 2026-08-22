'use strict';

const assert = require('node:assert/strict');
const test = require('node:test');

const issues = require('../lib/github-issue.js');

/**
 * A GitHub client with just the four calls the helper makes, recording what it
 * was asked to do. `open` is the repository's open issues, oldest first, in the
 * pages listForRepo would return them in.
 */
function fakeGithub(open = []) {
    const calls = { created: [], updated: [], comments: [], listed: [] };
    return {
        calls,
        rest: {
            issues: {
                listForRepo: async parameters => {
                    calls.listed.push(parameters);
                    const start = (parameters.page - 1) * parameters.per_page;
                    return { data: open.slice(start, start + parameters.per_page) };
                },
                create: async parameters => {
                    calls.created.push(parameters);
                    return { data: { number: 100 + calls.created.length, html_url: 'https://issue/new' } };
                },
                update: async parameters => {
                    calls.updated.push(parameters);
                    return { data: {} };
                },
                createComment: async parameters => {
                    calls.comments.push(parameters);
                    return { data: {} };
                },
            },
        },
    };
}

const MARKER = '<!-- broiler:privacy-test-pages:gaps -->';

test('a finding with no issue open for it is filed as a new issue', async () => {
    const github = fakeGithub([{ number: 1, body: 'unrelated', html_url: 'https://issue/1' }]);

    const result = await issues.createOrUpdateIssue({
        github, owner: 'o', repo: 'r', marker: MARKER,
        title: 'Privacy test pages: 4 probe(s) behind Chromium',
        body: `${MARKER}\n\nfour gaps`,
        runUrl: 'https://run/1',
    });

    assert.equal(result.action, 'created');
    assert.equal(github.calls.created.length, 1);
    assert.equal(github.calls.updated.length, 0);
    assert.match(github.calls.created[0].body, /four gaps/);
});

test('the issue already open for the finding is refreshed rather than duplicated', async () => {
    const github = fakeGithub([
        { number: 1, body: 'unrelated', html_url: 'https://issue/1' },
        { number: 7, body: `${MARKER}\n\nlast week`, html_url: 'https://issue/7' },
    ]);

    const result = await issues.createOrUpdateIssue({
        github, owner: 'o', repo: 'r', marker: MARKER,
        title: 'Privacy test pages: 5 probe(s) behind Chromium',
        body: `${MARKER}\n\nfive gaps`,
        runUrl: 'https://run/2',
    });

    assert.equal(result.action, 'updated');
    assert.equal(result.number, 7);
    assert.equal(github.calls.created.length, 0);
    assert.equal(github.calls.updated[0].issue_number, 7);
    assert.match(github.calls.updated[0].title, /5 probe/);
    assert.match(github.calls.updated[0].body, /five gaps/);
    assert.equal(github.calls.comments[0].issue_number, 7);
    assert.match(github.calls.comments[0].body, /https:\/\/run\/2/);
});

test('a pull request carrying the marker is never mistaken for the issue', async () => {
    const github = fakeGithub([
        { number: 9, body: MARKER, html_url: 'https://pr/9', pull_request: { url: 'https://pr/9' } },
    ]);

    const result = await issues.createOrUpdateIssue({
        github, owner: 'o', repo: 'r', marker: MARKER, title: 'title', body: MARKER,
    });

    assert.equal(result.action, 'created');
});

test('another kind of finding does not refresh this one', async () => {
    const github = fakeGithub([
        { number: 3, body: '<!-- broiler:privacy-test-pages:regressions -->', html_url: 'https://issue/3' },
    ]);

    const result = await issues.createOrUpdateIssue({
        github, owner: 'o', repo: 'r', marker: MARKER, title: 'title', body: MARKER,
    });

    assert.equal(result.action, 'created');
});

test('the marker is added to a body that lost it, so the next run still matches', async () => {
    const github = fakeGithub();

    await issues.createOrUpdateIssue({
        github, owner: 'o', repo: 'r', marker: MARKER, title: 'title', body: 'no marker here',
    });

    assert.ok(github.calls.created[0].body.includes(MARKER));
});

test('the oldest matching issue wins, so a hand-opened duplicate does not take over', async () => {
    const github = fakeGithub([
        { number: 4, body: MARKER, html_url: 'https://issue/4' },
        { number: 8, body: MARKER, html_url: 'https://issue/8' },
    ]);

    const found = await issues.findOpenIssueByMarker({ github, owner: 'o', repo: 'r', marker: MARKER });

    assert.equal(found.number, 4);
    assert.equal(github.calls.listed[0].direction, 'asc');
});

test('the search walks pages but stops at the bound', async () => {
    const many = Array.from({ length: issues.PER_PAGE * (issues.MAX_PAGES + 2) }, (unused, index) => ({
        number: index + 1,
        body: 'unrelated',
        html_url: `https://issue/${index + 1}`,
    }));
    const github = fakeGithub(many);

    const found = await issues.findOpenIssueByMarker({ github, owner: 'o', repo: 'r', marker: MARKER });

    assert.equal(found, null);
    assert.equal(github.calls.listed.length, issues.MAX_PAGES);
});

test('a finding with no marker is a programming error, not a duplicate issue', async () => {
    const github = fakeGithub();

    await assert.rejects(
        () => issues.findOpenIssueByMarker({ github, owner: 'o', repo: 'r', marker: '' }),
        /marker is required/);
});
