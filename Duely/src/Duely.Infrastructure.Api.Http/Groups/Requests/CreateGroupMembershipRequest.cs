// using System.ComponentModel.DataAnnotations;
// using System.Text.Json.Serialization;
// using Duely.Domain.Models.Groups.Entities;
//
// namespace Duely.Infrastructure.Api.Http.Groups.Requests;
//
// public sealed class CreateGroupMembershipRequest
// {
//     [JsonPropertyName("user_id"), Required]
//     public required Guid UserId { get; init; }
//
//     [JsonPropertyName("role"), Required, JsonConverter(typeof(JsonStringEnumConverter))]
//     public required GroupRole Role { get; init; }
// }
