ALTER TABLE "station" ADD COLUMN "pairing_code_hash" text;--> statement-breakpoint
ALTER TABLE "station" ADD COLUMN "pairing_code_expires_at" timestamp with time zone;