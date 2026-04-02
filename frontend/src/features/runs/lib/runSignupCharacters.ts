import { normalizeLocalizedString } from "../../../lib/localizedStrings";
import { normalizePortraitUrlField } from "../../../lib/portraitUrls";

export interface RunSignupCharacter {
  id: string;
  name: string;
  realm: string;
  classId: number;
  portraitUrl?: string;
  specializations?: Array<{ id: number; name: string; role: string }>;
  activeSpecId?: number | null;
}

export function normalizeRunSignupCharacter(character: RunSignupCharacter): RunSignupCharacter {
  return normalizePortraitUrlField({
    ...character,
    name: normalizeLocalizedString(character.name),
    realm: normalizeLocalizedString(character.realm),
    specializations: character.specializations?.map((specialization) => ({
      ...specialization,
      name: normalizeLocalizedString(specialization.name),
      role: normalizeLocalizedString(specialization.role),
    })),
  });
}
