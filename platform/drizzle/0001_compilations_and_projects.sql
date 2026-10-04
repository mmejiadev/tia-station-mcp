CREATE TABLE "compilation" (
	"id" bigint PRIMARY KEY GENERATED ALWAYS AS IDENTITY (sequence name "compilation_id_seq" INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START WITH 1 CACHE 1),
	"station_id" integer NOT NULL,
	"project_id" integer NOT NULL,
	"entry_key" text NOT NULL,
	"line_number" integer NOT NULL,
	"compiled_at" timestamp with time zone,
	"software_path" text NOT NULL,
	"severity" text NOT NULL,
	"error_count" integer NOT NULL,
	"warning_count" integer NOT NULL,
	"imported_at" timestamp with time zone DEFAULT now() NOT NULL,
	CONSTRAINT "compilation_station_entry_key" UNIQUE("station_id","entry_key")
);
--> statement-breakpoint
CREATE TABLE "compilation_message" (
	"id" bigint PRIMARY KEY GENERATED ALWAYS AS IDENTITY (sequence name "compilation_message_id_seq" INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START WITH 1 CACHE 1),
	"compilation_id" bigint NOT NULL,
	"position" integer NOT NULL,
	"severity" text NOT NULL,
	"path" text NOT NULL,
	"description" text NOT NULL,
	CONSTRAINT "compilation_message_position" UNIQUE("compilation_id","position")
);
--> statement-breakpoint
CREATE TABLE "project" (
	"id" integer PRIMARY KEY GENERATED ALWAYS AS IDENTITY (sequence name "project_id_seq" INCREMENT BY 1 MINVALUE 1 MAXVALUE 2147483647 START WITH 1 CACHE 1),
	"station_id" integer NOT NULL,
	"tia_path" text NOT NULL,
	"name" text NOT NULL,
	"tia_author" text,
	"tia_created_at" timestamp with time zone,
	"tia_modified_at" timestamp with time zone,
	"tia_modified_by" text,
	"last_seen_at" timestamp with time zone,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	CONSTRAINT "project_station_path" UNIQUE("station_id","tia_path")
);
--> statement-breakpoint
ALTER TABLE "compilation" ADD CONSTRAINT "compilation_station_id_station_id_fk" FOREIGN KEY ("station_id") REFERENCES "public"."station"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "compilation" ADD CONSTRAINT "compilation_project_id_project_id_fk" FOREIGN KEY ("project_id") REFERENCES "public"."project"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "compilation_message" ADD CONSTRAINT "compilation_message_compilation_id_compilation_id_fk" FOREIGN KEY ("compilation_id") REFERENCES "public"."compilation"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
ALTER TABLE "project" ADD CONSTRAINT "project_station_id_station_id_fk" FOREIGN KEY ("station_id") REFERENCES "public"."station"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
CREATE INDEX "compilation_project_compiled_at" ON "compilation" USING btree ("project_id","compiled_at");