import { describe, expect, it } from "vitest";
import { CREDIT_ROLE } from "$lib/entities/entity-codes";
import type { EntityDetailCard, EntityDetailCardFull } from "$lib/entities/entity-detail";
import { buildMetadataUpdate, draftFromCard, type EntityCreditDraft } from "./entity-detail-edit";

const FLAGS = { isFavorite: false, isNsfw: false, isOrganized: false };

function cardWithCredits(credits: EntityDetailCardFull["credits"]): EntityDetailCard {
  return {
    entity: { id: "e1", kind: "movie", title: "Movie", capabilities: [] },
    kindLabel: "Movie",
    hero: null,
    poster: null,
    posterCard: null,
    description: null,
    rating: null,
    flags: [],
    tags: [],
    links: [],
    files: [],
    presentCapabilities: [],
    credits,
    studio: null,
  } as unknown as EntityDetailCard;
}

describe("credits edit round-trip", () => {
  it("carries every role and character from the card into the draft", () => {
    const draft = draftFromCard(
      cardWithCredits([
        {
          id: "p1",
          kind: "person",
          title: "Jane Doe",
          thumbnail: "/people/jane.jpg",
          roles: ["director", "writer"],
          characters: ["Narrator", "Old Narrator"],
        },
      ]),
      FLAGS,
    );

    expect(draft.credits).toEqual([
      {
        name: "Jane Doe",
        thumbnailUrl: "/people/jane.jpg",
        roles: ["director", "writer"],
        character: "Narrator",
        extraCharacters: ["Old Narrator"],
      },
    ]);
  });

  it("expands multi-role credits into one patch row per role, keeping extra characters", () => {
    const credits: EntityCreditDraft[] = [
      {
        name: "Jane Doe",
        thumbnailUrl: null,
        roles: ["director", "writer"],
        character: "Narrator",
        extraCharacters: ["Old Narrator"],
      },
      { name: "John Smith", thumbnailUrl: null, roles: ["actor"], character: "", extraCharacters: [] },
    ];
    const draft = { ...draftFromCard(cardWithCredits([]), FLAGS), credits };

    const request = buildMetadataUpdate([{ id: "credits" }], draft);

    expect(request.fields).toEqual(["credits"]);
    expect(request.patch.credits).toEqual([
      { name: "Jane Doe", role: "director", character: "Narrator", sortOrder: 0 },
      { name: "Jane Doe", role: "writer", character: null, sortOrder: 0 },
      { name: "Jane Doe", role: "director", character: "Old Narrator", sortOrder: 0 },
      { name: "John Smith", role: "actor", character: null, sortOrder: 1 },
    ]);
  });

  it("falls back to the generic person role when a credit has no roles", () => {
    const draft = {
      ...draftFromCard(cardWithCredits([]), FLAGS),
      credits: [{ name: "Jane Doe", thumbnailUrl: null, roles: [], character: "", extraCharacters: [] }],
    };

    const request = buildMetadataUpdate([{ id: "credits" }], draft);

    expect(request.patch.credits).toEqual([
      { name: "Jane Doe", role: CREDIT_ROLE.person, character: null, sortOrder: 0 },
    ]);
  });

  it("drops blank names and skips extra characters that duplicate the primary", () => {
    const draft = {
      ...draftFromCard(cardWithCredits([]), FLAGS),
      credits: [
        { name: "  ", thumbnailUrl: null, roles: ["actor"], character: "X", extraCharacters: [] },
        {
          name: "Jane Doe",
          thumbnailUrl: null,
          roles: ["actor"],
          character: "Narrator",
          extraCharacters: ["narrator", "Captain"],
        },
      ],
    };

    const request = buildMetadataUpdate([{ id: "credits" }], draft);

    expect(request.patch.credits).toEqual([
      { name: "Jane Doe", role: "actor", character: "Narrator", sortOrder: 1 },
      { name: "Jane Doe", role: "actor", character: "Captain", sortOrder: 1 },
    ]);
  });

  it("excludes credits from the patch when the section is not in scope", () => {
    const draft = {
      ...draftFromCard(cardWithCredits([]), FLAGS),
      credits: [{ name: "Jane Doe", thumbnailUrl: null, roles: ["actor"], character: "", extraCharacters: [] }],
    };

    const request = buildMetadataUpdate([{ id: "description" }], draft);

    expect(request.fields).not.toContain("credits");
    expect(request.patch.credits).toEqual([]);
  });
});
