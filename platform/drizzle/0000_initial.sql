CREATE TABLE "change" (
	"id" bigint PRIMARY KEY GENERATED ALWAYS AS IDENTITY (sequence name "change_id_seq" INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START WITH 1 CACHE 1),
	"station_id" integer NOT NULL,
	"entry_key" text NOT NULL,
	"line_number" integer NOT NULL,
	"sequence" integer,
	"chain_hash" text,
	"timestamp" text NOT NULL,
	"occurred_at" timestamp with time zone,
	"plan_id" text NOT NULL,
	"mode" text NOT NULL,
	"tool" text NOT NULL,
	"target" text NOT NULL,
	"value" text NOT NULL,
	"backup_path" text NOT NULL,
	"origin" text NOT NULL,
	"outcome" text NOT NULL,
	"detail" text NOT NULL,
	"documentation" text NOT NULL,
	"imported_at" timestamp with time zone DEFAULT now() NOT NULL,
	CONSTRAINT "change_station_entry_key" UNIQUE("station_id","entry_key")
);
--> statement-breakpoint
CREATE TABLE "station" (
	"id" integer PRIMARY KEY GENERATED ALWAYS AS IDENTITY (sequence name "station_id_seq" INCREMENT BY 1 MINVALUE 1 MAXVALUE 2147483647 START WITH 1 CACHE 1),
	"name" text NOT NULL,
	"created_at" timestamp with time zone DEFAULT now() NOT NULL,
	CONSTRAINT "station_name_unique" UNIQUE("name")
);
--> statement-breakpoint
ALTER TABLE "change" ADD CONSTRAINT "change_station_id_station_id_fk" FOREIGN KEY ("station_id") REFERENCES "public"."station"("id") ON DELETE no action ON UPDATE no action;--> statement-breakpoint
CREATE INDEX "change_station_occurred_at" ON "change" USING btree ("station_id","occurred_at");