export interface LoginResponseDTO {
  name?: string;
  accessToken: string;
  refreshToken?: string;
  redirect?: string;
  battleNetId?: string;
  guildName?: string;
}
